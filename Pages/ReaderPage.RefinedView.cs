using System.Text.Json;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class ReaderPage
{
    private bool _refinedReaderUiInitialized;
    private bool _readerInputMessagesHooked;
    private CancellationTokenSource? _leftPaneAnimationCts;
    private CancellationTokenSource? _rightPaneAnimationCts;

    private void InitializeRefinedReaderUi()
    {
        if (_refinedReaderUiInitialized) return;
        _refinedReaderUiInitialized = true;

        // Figma 16:156 / 16:227 refined state: Aa is the only permanent reader tool.
        MoreButton.Visibility = Visibility.Collapsed;
        MoreButton.IsEnabled = false;
        MoreButton.Flyout = null;
        if (MoreButton.Parent is StackPanel tools)
        {
            tools.Width = 40;
            tools.Spacing = 0;
            tools.Margin = new Thickness(0, 8, 24, 8);
        }
        BookTitleText.Margin = new Thickness(64, 0, 76, 0);

        // Visible arrow chrome is intentionally removed. Paging is driven by wheel/click/keyboard.
        PreviousPageButton.Visibility = Visibility.Collapsed;
        PreviousPageButton.IsHitTestVisible = false;
        NextPageButton.Visibility = Visibility.Collapsed;
        NextPageButton.IsHitTestVisible = false;

        SimplifySelectionNoteEditor();

        ReaderWebView.NavigationCompleted += async (_, args) =>
        {
            if (!args.IsSuccess || ReaderWebView.CoreWebView2 is null) return;
            if (_pageMeasurementInProgress) return;
            HookReaderInputMessages();
            await InstallReaderInputBridgeAsync();
            await ApplyReaderViewEnhancementsAsync();
            // The existing annotation hook may still render legacy highlight-only records first.
            // Re-apply the current note-only contract after the navigation callbacks settle.
            await Task.Delay(40);
            await ApplyNoteOnlyHighlightsAsync();
        };
    }

    private async void ReaderViewOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;

        var reloadReaderUnit = false;
        var frameOnlyChange = false;
        if (tag is "spread:single" or "spread:odd" or "spread:even")
        {
            reloadReaderUnit = true;
            App.Settings.Update(settings =>
            {
                if (tag == "spread:single")
                {
                    settings.ReaderSpreadMode = "single";
                    settings.ReaderViewMode = "horizontal";
                }
                else
                {
                    settings.ReaderSpreadMode = tag[7..];
                    settings.ReaderViewMode = "horizontal";
                }
            });
        }
        else if (tag == "zoom:fit-width" || tag == "zoom:fit-height")
        {
            frameOnlyChange = true;
            App.Settings.Update(settings =>
            {
                settings.ReaderZoomMode = tag[5..];
            });
        }

        EnforceFixedReaderOptions();
        UpdateReaderViewOptionSelection();
        ApplyFigmaReaderPageGeometry();
        if (frameOnlyChange) return;
        if (reloadReaderUnit && _document is not null && _source is not null)
        {
            // A spread change creates a different page group. Reusing the old
            // fractional column offset can land between the previous and the
            // newly paired pages, exposing a sliver of the preceding page.
            await NavigateToSectionAsync(_sectionIndex, restoreSavedFraction: false);
            return;
        }
        if (_webReady)
        {
            var progress = _sectionFraction;
            await ApplyWebReaderStyleAsync(progress);
            ApplyFigmaReaderPageGeometry();
            await ApplyReaderViewEnhancementsAsync();
            await ApplyNoteOnlyHighlightsAsync();
        }
    }

    private void UpdateReaderViewOptionSelection()
    {
        var settings = App.Settings.Current;
        SetSegmentSelection(SinglePageButton, settings.ReaderViewMode == "horizontal" && settings.ReaderSpreadMode == "single");
        SetSegmentSelection(OddPageStartButton, settings.ReaderSpreadMode == "odd");
        SetSegmentSelection(EvenPageStartButton, settings.ReaderSpreadMode == "even");
        SetSegmentSelection(FitPageWidthButton, settings.ReaderZoomMode == "fit-width");
        SetSegmentSelection(FitPageHeightButton, settings.ReaderZoomMode == "fit-height");
    }

    private void SimplifySelectionNoteEditor()
    {
        SelectionAnnotationCard.Width = 360;
        SelectionAnnotationCard.Height = 74;
        SelectionAnnotationCard.Padding = new Thickness(10);
        SelectionAnnotationTextBox.Height = 52;
        SelectionAnnotationTextBox.MinHeight = 52;
        SelectionAnnotationTextBox.MaxHeight = 52;
        SelectionAnnotationTextBox.PlaceholderText = ReaderText(
            "为所选文字添加笔记…",
            "選択したテキストにノートを追加…",
            "Add a note to the selection…");
    }

    private async Task AnimateLeftSidebarAsync(bool open)
    {
        _leftPaneAnimationCts?.Cancel();
        _leftPaneAnimationCts?.Dispose();
        _leftPaneAnimationCts = new CancellationTokenSource();
        try
        {
            await AnimateReaderColumnAsync(ContentsColumn, ContentsPane, open ? 260 : 0, _leftPaneAnimationCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task AnimateRightSidebarAsync(bool open)
    {
        _rightPaneAnimationCts?.Cancel();
        _rightPaneAnimationCts?.Dispose();
        _rightPaneAnimationCts = new CancellationTokenSource();
        try
        {
            await AnimateReaderColumnAsync(SettingsColumn, ReaderSettingsPane, open ? 260 : 0, _rightPaneAnimationCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task AnimateReaderColumnAsync(
        ColumnDefinition column,
        FrameworkElement pane,
        double targetWidth,
        CancellationToken cancellationToken)
    {
        var startWidth = column.Width.GridUnitType == GridUnitType.Pixel ? column.Width.Value : 0;
        if (Math.Abs(startWidth - targetWidth) < 0.5)
        {
            column.Width = new GridLength(targetWidth);
            pane.Visibility = targetWidth > 0 ? Visibility.Visible : Visibility.Collapsed;
            pane.Opacity = 1;
            return;
        }

        if (targetWidth > 0)
        {
            pane.Visibility = Visibility.Visible;
            pane.Opacity = startWidth <= 0.5 ? 0 : 1;
        }

        const int steps = 12;
        for (var step = 1; step <= steps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var t = step / (double)steps;
            var eased = 1 - Math.Pow(1 - t, 3);
            var width = startWidth + ((targetWidth - startWidth) * eased);
            column.Width = new GridLength(Math.Max(0, width));
            pane.Opacity = targetWidth > startWidth ? Math.Clamp(eased * 1.15, 0, 1) : Math.Clamp(1 - eased * 0.45, 0, 1);
            ApplyFigmaReaderPageGeometry();
            await Task.Delay(15, cancellationToken);
        }

        column.Width = new GridLength(targetWidth);
        pane.Opacity = 1;
        if (targetWidth <= 0.5) pane.Visibility = Visibility.Collapsed;
        ApplyFigmaReaderPageGeometry();
    }

    private void HookReaderInputMessages()
    {
        if (_readerInputMessagesHooked || ReaderWebView.CoreWebView2 is null) return;
        _readerInputMessagesHooked = true;
        ReaderWebView.CoreWebView2.WebMessageReceived += (core, args) =>
        {
            var message = args.TryGetWebMessageAsString();
            if (message.StartsWith("pagearc-zoom:", StringComparison.Ordinal))
            {
                if (int.TryParse(message.AsSpan("pagearc-zoom:".Length), out var zoomDelta))
                    _ = ChangeReaderZoomAsync(zoomDelta);
                return;
            }
            if (!message.StartsWith("pagearc-turn:", StringComparison.Ordinal)) return;
            if (!int.TryParse(message.AsSpan("pagearc-turn:".Length), out var delta)) return;
            _ = TurnPageByDeltaAsync(delta < 0 ? -1 : 1);
        };
    }

    private Task ChangeReaderZoomAsync(int delta)
    {
        var current = App.Settings.Current.ReaderZoomMode == "custom"
            ? App.Settings.Current.ReaderZoomFactor
            : 1d;
        var next = delta == 0 ? 1d : Math.Clamp(current + (delta > 0 ? 0.1d : -0.1d), 0.6d, 2d);
        App.Settings.Update(settings =>
        {
            settings.ReaderZoomMode = delta == 0 ? "auto" : "custom";
            settings.ReaderZoomFactor = next;
        });
        UpdateReaderViewOptionSelection();
        ApplyFigmaReaderPageGeometry();
        return Task.CompletedTask;
    }

    private async Task InstallReaderInputBridgeAsync()
    {
        if (ReaderWebView.CoreWebView2 is null) return;
        var script = """
            (() => {
              window.__pagearcClickToTurn = __CLICK_TO_TURN__;
              if (window.__pagearcInputBridgeInstalled) return true;
              window.__pagearcInputBridgeInstalled = true;
              const hasSelection = () => !!(window.getSelection?.().toString?.().trim());
              const interactive = target => target?.closest?.('a,button,input,textarea,select,summary,[contenteditable="true"],label');
              const postBoundaryTurn = delta => window.chrome?.webview?.postMessage('pagearc-turn:' + delta);
              const postZoom = delta => window.chrome?.webview?.postMessage('pagearc-zoom:' + delta);
              const turn = delta => {
                if (hasSelection()) return;
                try {
                  if (window.__pagearc?.move?.(delta)) return;
                } catch {}
                postBoundaryTurn(delta);
              };
              let wheelReady = true;
              document.addEventListener('wheel', event => {
                if (event.ctrlKey) {
                  event.preventDefault();
                  postZoom(event.deltaY < 0 ? 1 : -1);
                  return;
                }
                const mode = window.__pagearcReaderViewMode || 'horizontal';
                if (mode === 'vertical') return;
                const amount = Math.abs(event.deltaY) >= Math.abs(event.deltaX) ? event.deltaY : event.deltaX;
                if (Math.abs(amount) < 8) return;
                event.preventDefault();
                if (!wheelReady) return;
                wheelReady = false;
                setTimeout(() => wheelReady = true, 180);
                turn(amount > 0 ? 1 : -1);
              }, {passive:false, capture:true});
              document.addEventListener('keydown', event => {
                if (!event.ctrlKey) return;
                const key = event.key;
                if (key !== '+' && key !== '=' && key !== '-' && key !== '_' && key !== '0') return;
                event.preventDefault();
                postZoom(key === '0' ? 0 : (key === '-' || key === '_' ? -1 : 1));
              }, true);
              document.addEventListener('click', event => {
                if (window.__pagearcClickToTurn === false || interactive(event.target) || hasSelection()) return;
                const rtl = getComputedStyle(document.documentElement).direction === 'rtl' || getComputedStyle(document.body).direction === 'rtl';
                const leftHalf = event.clientX < window.innerWidth / 2;
                const delta = rtl ? (leftHalf ? 1 : -1) : (leftHalf ? -1 : 1);
                turn(delta);
              }, true);
              return true;
            })()
            """.Replace(
                "__CLICK_TO_TURN__",
                App.Settings.Current.ClickToTurnPages ? "true" : "false",
                StringComparison.Ordinal);
        try
        {
            await ReaderWebView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Reader input bridge injection failed", ex);
        }
    }

    private async Task TurnPageByDeltaAsync(int delta)
    {
        if (_document is null || delta == 0) return;
        if (await TryMoveWithinSectionAsync(delta)) return;

        if (delta < 0 && _sectionIndex > 0)
        {
            _sectionFraction = 1;
            await NavigateToSectionAsync(PreviousReaderSectionIndex(), restoreSavedFraction: true);
        }
        else if (delta > 0 && NextReaderSectionIndex() < _document.Sections.Count)
        {
            _sectionFraction = 0;
            await NavigateToSectionAsync(NextReaderSectionIndex(), restoreSavedFraction: true);
        }
    }

    private async Task ApplyReaderViewEnhancementsAsync(string? spreadOverride = null)
    {
        if (!_webReady || ReaderWebView.CoreWebView2 is null) return;
        var settings = App.Settings.Current;
        var mode = JsonSerializer.Serialize(settings.ReaderViewMode);
        var spread = JsonSerializer.Serialize(spreadOverride ?? settings.ReaderSpreadMode);
        var script = """
            (() => {
              const mode = __MODE__;
              const spread = __SPREAD__;
              window.__pagearcReaderViewMode = mode;
              const root = document.scrollingElement || document.documentElement;
              const body = document.body;
              const previousState = window.__pagearc;
              const previousPage = previousState && mode === 'horizontal'
                ? Math.round(root.scrollLeft / Math.max(1, previousState.pageStep || root.clientWidth))
                : 0;
              const previousProgress = previousState?.progress?.() ?? 0;
              const media = [...body.querySelectorAll('img,svg,video')];
              const imagePage = media.length > 0 && (body.innerText || '').trim().length < 80;
              body.classList.toggle('pagearc-image-page', imagePage);
              let style = document.getElementById('pagearc-view-mode-style');
              if (!style) { style = document.createElement('style'); style.id = 'pagearc-view-mode-style'; document.head.appendChild(style); }
              let extra = '';
              if (mode === 'horizontal') {
                extra += spread === 'single'
                  ? 'html{overflow:hidden!important;overscroll-behavior:none!important;}body{box-sizing:border-box!important;width:100%!important;height:100%!important;min-width:0!important;max-width:none!important;padding:44px 56px 56px!important;overflow:visible!important;column-count:1!important;column-width:auto!important;column-gap:112px!important;column-fill:auto!important;}body>*{max-inline-size:100%!important;}'
                  : 'html{overflow:hidden!important;overscroll-behavior:none!important;}body{box-sizing:border-box!important;width:100%!important;height:100%!important;min-width:0!important;max-width:none!important;padding:44px 42px 56px!important;overflow:visible!important;column-count:2!important;column-width:auto!important;column-gap:84px!important;column-fill:auto!important;}body>.pagearc-spread-page{box-sizing:border-box;}body>.pagearc-spread-right{break-before:column;-webkit-column-break-before:always;}body>.pagearc-spread-blank{height:calc(100vh - 100px);break-after:column;-webkit-column-break-after:always;}';
              }
              if (mode === 'wrapped') {
                extra += 'body{max-width:none!important;}';
              }
              if (imagePage) {
                extra += 'body.pagearc-image-page img,body.pagearc-image-page svg,body.pagearc-image-page video{display:block!important;width:100%!important;height:auto!important;max-height:none!important;margin:auto!important;object-fit:contain!important;}';
              }
              style.textContent = extra;
              if (mode === 'horizontal') {
                const horizontalPadding = spread === 'single' ? 112 : 84;
                const columnGap = spread === 'single' ? 112 : 84;
                const visibleColumns = spread === 'single' ? 1 : 2;
                const contentWidth = Math.max(1, root.clientWidth - horizontalPadding);
                const columnWidth = spread === 'single'
                  ? contentWidth
                  : Math.max(1, (contentWidth - columnGap) / 2);
                const pageStep = (columnWidth + columnGap) * visibleColumns;
                body.style.setProperty('width', root.clientWidth + 'px', 'important');
                body.style.setProperty('column-width', columnWidth + 'px', 'important');
                body.style.setProperty('column-count', visibleColumns.toString(), 'important');
                body.style.setProperty('column-gap', columnGap + 'px', 'important');
                if (window.__pagearc) {
                  window.__pagearc.visiblePageCount = visibleColumns;
                  window.__pagearc.pageStep = pageStep;
                  window.__pagearc.snap?.();
                }
              }
              body.style.zoom = '1';
              requestAnimationFrame(() => {
                requestAnimationFrame(() => {
                  if (mode === 'horizontal') window.__pagearc?.restorePage?.(previousPage);
                  else window.__pagearc?.restore?.(previousProgress);
                });
              });
              return 1;
            })()
            """
            .Replace("__MODE__", mode, StringComparison.Ordinal)
            .Replace("__SPREAD__", spread, StringComparison.Ordinal);
        try
        {
            await ReaderWebView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Reader view-mode enhancement failed", ex);
        }
    }

    private async void ClickPageTurnToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_figmaReaderControlsReady) return;
        var enabled = ClickPageTurnToggle.IsOn;
        App.Settings.Update(settings => settings.ClickToTurnPages = enabled);
        if (ReaderWebView.CoreWebView2 is null) return;

        try
        {
            await ReaderWebView.CoreWebView2.ExecuteScriptAsync(
                $"window.__pagearcClickToTurn = {(enabled ? "true" : "false")};");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Updating click-to-turn setting failed", ex);
        }
    }

    private async Task ApplyNoteOnlyHighlightsAsync()
    {
        if (!_webReady || ReaderWebView.CoreWebView2 is null || _book is null) return;
        var annotations = App.ReadingData.GetAnnotations(_book.Id)
            .Where(item => item.Locator.SectionIndex == _sectionIndex
                           && !string.IsNullOrWhiteSpace(item.Note)
                           && !string.IsNullOrWhiteSpace(item.Quote))
            .Select(item => new { id = item.Id, quote = item.Quote })
            .ToArray();
        var payload = JsonSerializer.Serialize(annotations);
        var script = """
            (() => {
              document.querySelectorAll('mark.pagearc-annotation').forEach(mark => {
                const parent = mark.parentNode;
                if (!parent) return;
                parent.replaceChild(document.createTextNode(mark.textContent || ''), mark);
                parent.normalize();
              });
              const annotations = __ANNOTATIONS__;
              let applied = 0;
              for (const item of annotations) {
                const needle = (item.quote || '').trim();
                if (!needle) continue;
                const lowerNeedle = needle.toLocaleLowerCase();
                const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
                let node;
                while ((node = walker.nextNode())) {
                  const parent = node.parentElement;
                  if (!parent || parent.closest('script,style,mark.pagearc-annotation')) continue;
                  const source = node.nodeValue || '';
                  const index = source.toLocaleLowerCase().indexOf(lowerNeedle);
                  if (index < 0) continue;
                  const range = document.createRange();
                  range.setStart(node, index);
                  range.setEnd(node, index + needle.length);
                  const mark = document.createElement('mark');
                  mark.className = 'pagearc-annotation';
                  mark.dataset.pagearcAnnotationId = item.id;
                  mark.style.background = 'rgba(185,111,111,.30)';
                  mark.style.color = 'inherit';
                  mark.style.borderRadius = '2px';
                  mark.style.padding = '0 1px';
                  range.surroundContents(mark);
                  applied++;
                  break;
                }
              }
              return applied;
            })()
            """.Replace("__ANNOTATIONS__", payload, StringComparison.Ordinal);
        try
        {
            await ReaderWebView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Reader note-only highlight rendering failed", ex);
        }
    }
}
