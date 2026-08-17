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
    private TextBlock? _readerViewModeValueText;

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

        // Keep the old toggle as an invisible migration bridge because older settings files store it,
        // while the new View mode selector becomes the only visible scrolling control.
        if (ContinuousScrollLabel.Parent is FrameworkElement legacyContinuousRow)
            legacyContinuousRow.Visibility = Visibility.Collapsed;

        BuildReaderViewModeControls();
        SimplifySelectionNoteEditor();

        ReaderWebView.NavigationCompleted += async (_, args) =>
        {
            if (!args.IsSuccess || ReaderWebView.CoreWebView2 is null) return;
            HookReaderInputMessages();
            await InstallReaderInputBridgeAsync();
            await ApplyReaderViewEnhancementsAsync();
            // The existing annotation hook may still render legacy highlight-only records first.
            // Re-apply the current note-only contract after the navigation callbacks settle.
            await Task.Delay(40);
            await ApplyNoteOnlyHighlightsAsync();
        };
    }

    private void BuildReaderViewModeControls()
    {
        if (ReaderBehaviorLabel.Parent is not StackPanel settingsPanel) return;

        ReaderBehaviorLabel.Text = ReaderText("阅读选项", "読書オプション", "Reading options");
        ReaderBehaviorLabel.Margin = new Thickness(0, 8, 0, 0);

        var viewLabel = new TextBlock
        {
            Text = ReaderText("查看方式", "表示方法", "View mode"),
            Margin = new Thickness(0, 4, 0, 0),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.72
        };
        _readerViewModeValueText = new TextBlock
        {
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var chevron = new FontIcon
        {
            Glyph = "\uE70D",
            FontSize = 10,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var content = new Grid { ColumnSpacing = 8 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(_readerViewModeValueText);
        Grid.SetColumn(chevron, 1);
        content.Children.Add(chevron);

        var button = new Button
        {
            Height = 34,
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(4),
            Content = content
        };
        var menu = new MenuFlyout();
        AddViewMenuItem(menu, ReaderText("垂直滚动", "垂直スクロール", "Vertical scrolling"), "view:vertical");
        AddViewMenuItem(menu, ReaderText("水平滚动", "水平スクロール", "Horizontal scrolling"), "view:horizontal");
        AddViewMenuItem(menu, ReaderText("覆盖滚动", "折り返しスクロール", "Wrapped scrolling"), "view:wrapped");
        menu.Items.Add(new MenuFlyoutSeparator());
        AddViewMenuItem(menu, ReaderText("不平铺", "単ページ", "No spread"), "spread:single");
        AddViewMenuItem(menu, ReaderText("奇数页起始（无封面）", "奇数ページ開始（表紙なし）", "Odd-page start (no cover)"), "spread:odd");
        AddViewMenuItem(menu, ReaderText("偶数页起始（有封面）", "偶数ページ開始（表紙あり）", "Even-page start (with cover)"), "spread:even");
        menu.Items.Add(new MenuFlyoutSeparator());
        AddViewMenuItem(menu, ReaderText("放大", "拡大", "Zoom in"), "zoom:in");
        AddViewMenuItem(menu, ReaderText("缩小", "縮小", "Zoom out"), "zoom:out");
        AddViewMenuItem(menu, ReaderText("自动调整大小", "自動調整", "Automatic sizing"), "zoom:auto");
        AddViewMenuItem(menu, ReaderText("适应页面宽度", "ページ幅に合わせる", "Fit page width"), "zoom:fit-width");
        AddViewMenuItem(menu, ReaderText("适应页面高度", "ページ高さに合わせる", "Fit page height"), "zoom:fit-height");
        button.Flyout = menu;

        var insertAt = settingsPanel.Children.IndexOf(ReaderBehaviorLabel);
        if (insertAt < 0) insertAt = settingsPanel.Children.Count;
        settingsPanel.Children.Insert(insertAt, viewLabel);
        settingsPanel.Children.Insert(insertAt + 1, button);
        UpdateReaderViewModeSummary();
    }

    private void AddViewMenuItem(MenuFlyout menu, string label, string tag)
    {
        var item = new MenuFlyoutItem { Text = label, Tag = tag };
        item.Click += ReaderViewModeItem_Click;
        menu.Items.Add(item);
    }

    private async void ReaderViewModeItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string tag }) return;

        if (tag.StartsWith("view:", StringComparison.Ordinal))
        {
            var mode = tag[5..] switch
            {
                "vertical" => "vertical",
                "wrapped" => "wrapped",
                _ => "horizontal"
            };
            App.Settings.Update(settings =>
            {
                settings.ReaderViewMode = mode;
                settings.ContinuousScrolling = mode is not "horizontal";
            });
        }
        else if (tag.StartsWith("spread:", StringComparison.Ordinal))
        {
            var spread = tag[7..] switch
            {
                "odd" => "odd",
                "even" => "even",
                _ => "single"
            };
            App.Settings.Update(settings => settings.ReaderSpreadMode = spread);
        }
        else if (tag == "zoom:in")
        {
            App.Settings.Update(settings =>
            {
                settings.ReaderZoomMode = "custom";
                settings.ReaderZoomFactor = Math.Clamp(settings.ReaderZoomFactor + 0.1, 0.6, 2.0);
            });
        }
        else if (tag == "zoom:out")
        {
            App.Settings.Update(settings =>
            {
                settings.ReaderZoomMode = "custom";
                settings.ReaderZoomFactor = Math.Clamp(settings.ReaderZoomFactor - 0.1, 0.6, 2.0);
            });
        }
        else if (tag == "zoom:auto")
        {
            App.Settings.Update(settings =>
            {
                settings.ReaderZoomMode = "auto";
                settings.ReaderZoomFactor = 1.0;
            });
        }
        else if (tag == "zoom:fit-width")
        {
            App.Settings.Update(settings => settings.ReaderZoomMode = "fit-width");
        }
        else if (tag == "zoom:fit-height")
        {
            App.Settings.Update(settings => settings.ReaderZoomMode = "fit-height");
        }

        var previousSettingsReady = _settingsReady;
        _settingsReady = false;
        try
        {
            ContinuousScrollToggle.IsOn = App.Settings.Current.ReaderViewMode is not "horizontal";
        }
        finally
        {
            _settingsReady = previousSettingsReady;
        }

        UpdateReaderViewModeSummary();
        ApplyFigmaReaderPageGeometry();
        if (_webReady)
        {
            var progress = _sectionFraction;
            await ApplyWebReaderStyleAsync(progress);
            ApplyFigmaReaderPageGeometry();
            await ApplyReaderViewEnhancementsAsync();
            await ApplyNoteOnlyHighlightsAsync();
        }
    }

    private void UpdateReaderViewModeSummary()
    {
        if (_readerViewModeValueText is null) return;
        _readerViewModeValueText.Text = App.Settings.Current.ReaderViewMode switch
        {
            "vertical" => ReaderText("垂直滚动", "垂直スクロール", "Vertical scrolling"),
            "wrapped" => ReaderText("覆盖滚动", "折り返しスクロール", "Wrapped scrolling"),
            _ => ReaderText("水平滚动", "水平スクロール", "Horizontal scrolling")
        };
    }

    private void SimplifySelectionNoteEditor()
    {
        AnnotationHighlightLabel.Visibility = Visibility.Collapsed;
        HighlightYellowButton.Visibility = Visibility.Collapsed;
        HighlightBlueButton.Visibility = Visibility.Collapsed;
        HighlightGreenButton.Visibility = Visibility.Collapsed;
        AnnotationHintText.Visibility = Visibility.Collapsed;
        SelectionAnnotationCard.Width = 404;
        SelectionAnnotationCard.Padding = new Thickness(14, 12);
        SelectionAnnotationTextBox.MinHeight = 76;
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
        ReaderWebView.CoreWebView2.WebMessageReceived += (_, args) =>
        {
            var message = args.TryGetWebMessageAsString();
            if (!message.StartsWith("pagearc-turn:", StringComparison.Ordinal)) return;
            if (!int.TryParse(message.AsSpan("pagearc-turn:".Length), out var delta)) return;
            _ = TurnPageByDeltaAsync(delta < 0 ? -1 : 1);
        };
    }

    private async Task InstallReaderInputBridgeAsync()
    {
        if (ReaderWebView.CoreWebView2 is null) return;
        const string script = """
            (() => {
              if (window.__pagearcInputBridgeInstalled) return true;
              window.__pagearcInputBridgeInstalled = true;
              const hasSelection = () => !!(window.getSelection?.().toString?.().trim());
              const interactive = target => target?.closest?.('a,button,input,textarea,select,summary,[contenteditable="true"],label');
              const postBoundaryTurn = delta => window.chrome?.webview?.postMessage('pagearc-turn:' + delta);
              const turn = delta => {
                if (hasSelection()) return;
                try {
                  if (window.__pagearc?.move?.(delta)) return;
                } catch {}
                postBoundaryTurn(delta);
              };
              let wheelReady = true;
              document.addEventListener('wheel', event => {
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
              document.addEventListener('click', event => {
                if (interactive(event.target) || hasSelection()) return;
                const rtl = getComputedStyle(document.documentElement).direction === 'rtl' || getComputedStyle(document.body).direction === 'rtl';
                const leftHalf = event.clientX < window.innerWidth / 2;
                const delta = rtl ? (leftHalf ? 1 : -1) : (leftHalf ? -1 : 1);
                turn(delta);
              }, true);
              return true;
            })()
            """;
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
            await NavigateToSectionAsync(_sectionIndex - 1, restoreSavedFraction: true);
        }
        else if (delta > 0 && _sectionIndex + 1 < _document.Sections.Count)
        {
            _sectionFraction = 0;
            await NavigateToSectionAsync(_sectionIndex + 1, restoreSavedFraction: true);
        }
    }

    private async Task ApplyReaderViewEnhancementsAsync()
    {
        if (!_webReady || ReaderWebView.CoreWebView2 is null) return;
        var settings = App.Settings.Current;
        var mode = JsonSerializer.Serialize(settings.ReaderViewMode);
        var spread = JsonSerializer.Serialize(settings.ReaderSpreadMode);
        var zoomMode = JsonSerializer.Serialize(settings.ReaderZoomMode);
        var zoom = Math.Clamp(settings.ReaderZoomFactor, 0.6, 2.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var script = """
            (() => {
              const mode = __MODE__;
              const spread = __SPREAD__;
              const zoomMode = __ZOOM_MODE__;
              const requestedZoom = __ZOOM__;
              window.__pagearcReaderViewMode = mode;
              let style = document.getElementById('pagearc-view-mode-style');
              if (!style) { style = document.createElement('style'); style.id = 'pagearc-view-mode-style'; document.head.appendChild(style); }
              let extra = '';
              if (mode === 'horizontal' && spread !== 'single') {
                extra += 'body{column-width:calc(50vw - 70px)!important;column-gap:84px!important;}';
                if (spread === 'even') extra += 'body{padding-left:calc(50vw - 42px)!important;}';
              }
              if (mode === 'wrapped') {
                extra += 'body{max-width:none!important;}';
              }
              style.textContent = extra;

              const root = document.scrollingElement || document.documentElement;
              const body = document.body;
              body.style.zoom = '1';
              let factor = requestedZoom;
              if (zoomMode === 'fit-width') {
                factor = Math.max(.6, Math.min(2, root.clientWidth / Math.max(1, body.scrollWidth)));
              } else if (zoomMode === 'fit-height') {
                factor = Math.max(.6, Math.min(2, root.clientHeight / Math.max(1, body.scrollHeight)));
              } else if (zoomMode === 'auto') {
                factor = 1;
              }
              body.style.zoom = String(factor);
              const saved = window.__pagearc?.progress?.() ?? 0;
              requestAnimationFrame(() => window.__pagearc?.restore?.(saved));
              return factor;
            })()
            """
            .Replace("__MODE__", mode, StringComparison.Ordinal)
            .Replace("__SPREAD__", spread, StringComparison.Ordinal)
            .Replace("__ZOOM_MODE__", zoomMode, StringComparison.Ordinal)
            .Replace("__ZOOM__", zoom, StringComparison.Ordinal);
        try
        {
            await ReaderWebView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Reader view-mode enhancement failed", ex);
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
