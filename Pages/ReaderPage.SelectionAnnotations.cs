using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using PageArc.Services;
using Windows.Foundation;

namespace PageArc.Pages;

public sealed partial class ReaderPage
{
    private sealed class SelectionPopupPayload
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }

        [JsonPropertyName("annotationId")]
        public string? AnnotationId { get; set; }
    }

    private bool _selectionAnnotationUiReady;
    private bool _selectionMessageHooked;
    private bool _selectionTextInternalUpdate;
    private bool _selectionPopupClosing;
    private string _selectionQuote = string.Empty;
    private string? _selectionAnnotationId;
    private CancellationTokenSource? _selectionAutoSaveCts;

    private void InitializeSelectionAnnotationUi()
    {
        if (_selectionAnnotationUiReady) return;
        _selectionAnnotationUiReady = true;

        // Selection notes are a single autosaving field with light dismiss.
        MoreButton.Flyout = null;
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
        SelectionAnnotationPopup.IsLightDismissEnabled = true;
        SelectionAnnotationTextBox.TextChanged += SelectionAnnotationTextBox_TextChanged;
        SelectionAnnotationPopup.Closed += async (_, _) =>
        {
            if (_selectionPopupClosing) return;
            await FlushSelectionNoteAsync(refreshHighlight: true);
            ClearSelectionPopupState();
        };

        FilterAnnotationItemsToNotes();
        NotesModeButton.Click += (_, _) => DispatcherQueue.TryEnqueue(FilterAnnotationItemsToNotes);

        ReaderWebView.NavigationCompleted += async (_, args) =>
        {
            if (!args.IsSuccess || ReaderWebView.CoreWebView2 is null) return;
            if (!_selectionMessageHooked)
            {
                _selectionMessageHooked = true;
                ReaderWebView.CoreWebView2.WebMessageReceived += (_, messageArgs) =>
                    HandleSelectionWebMessage(messageArgs.TryGetWebMessageAsString());
            }
            await InstallSelectionObserverAsync();
        };
    }

    private async Task InstallSelectionObserverAsync()
    {
        if (ReaderWebView.CoreWebView2 is null) return;
        const string script = """
            (() => {
              if (window.__pagearcSelectionObserverInstalled) return true;
              window.__pagearcSelectionObserverInstalled = true;
              const post = value => window.chrome?.webview?.postMessage(value);
              const report = () => {
                const selection = window.getSelection?.();
                const text = (selection?.toString?.() || '').trim();
                if (!selection || selection.rangeCount === 0 || !text) {
                  post('selection-clear');
                  return;
                }
                const range = selection.getRangeAt(0);
                const rect = range.getBoundingClientRect();
                post('selection:' + JSON.stringify({
                  text: text.slice(0, 2000),
                  x: rect.x,
                  y: rect.y,
                  width: rect.width,
                  height: rect.height
                }));
              };
              document.addEventListener('mouseup', event => {
                if (event.target?.closest?.('mark.pagearc-annotation[data-pagearc-annotation-id]')) return;
                setTimeout(report, 0);
              }, true);
              document.addEventListener('click', event => {
                const mark = event.target?.closest?.('mark.pagearc-annotation[data-pagearc-annotation-id]');
                if (!mark) return;
                event.preventDefault();
                event.stopImmediatePropagation();
                const rect = mark.getBoundingClientRect();
                post('annotation-edit:' + JSON.stringify({
                  annotationId: mark.dataset.pagearcAnnotationId,
                  text: (mark.textContent || '').trim().slice(0, 2000),
                  x: rect.x,
                  y: rect.y,
                  width: rect.width,
                  height: rect.height
                }));
              }, true);
              document.addEventListener('keyup', event => {
                if (event.shiftKey || event.key.startsWith('Arrow')) setTimeout(report, 0);
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
            StartupDiagnostics.Log("Reader selection observer injection failed", ex);
        }
    }

    private async void HandleSelectionWebMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (string.Equals(message, "selection-clear", StringComparison.Ordinal))
        {
            await CloseSelectionPopupAsync(saveBeforeClose: true);
            return;
        }
        if (message.StartsWith("annotation-edit:", StringComparison.Ordinal))
        {
            try
            {
                var payload = JsonSerializer.Deserialize<SelectionPopupPayload>(message["annotation-edit:".Length..]);
                if (payload is null || string.IsNullOrWhiteSpace(payload.AnnotationId) || _book is null) return;
                var annotation = App.ReadingData.GetAnnotations(_book.Id)
                    .FirstOrDefault(item => string.Equals(item.Id, payload.AnnotationId, StringComparison.Ordinal));
                if (annotation is null || string.IsNullOrWhiteSpace(annotation.Note)) return;
                payload.Text = annotation.Quote;
                ShowSelectionAnnotationPopup(payload, annotation);
            }
            catch (Exception ex)
            {
                StartupDiagnostics.Log("Reader annotation edit message parsing failed", ex);
            }
            return;
        }
        if (!message.StartsWith("selection:", StringComparison.Ordinal)) return;

        try
        {
            var payload = JsonSerializer.Deserialize<SelectionPopupPayload>(message["selection:".Length..]);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Text)) return;

            var nextQuote = payload.Text.Trim();
            if (SelectionAnnotationPopup.IsOpen
                && !string.IsNullOrWhiteSpace(_selectionQuote)
                && !string.Equals(_selectionQuote, nextQuote, StringComparison.Ordinal))
            {
                await FlushSelectionNoteAsync(refreshHighlight: true);
            }
            ShowSelectionAnnotationPopup(payload);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Reader selection message parsing failed", ex);
        }
    }

    private void ShowSelectionAnnotationPopup(SelectionPopupPayload payload, ReaderAnnotation? selectedAnnotation = null)
    {
        var quote = payload.Text.Trim();
        if (SelectionAnnotationPopup.IsOpen && string.Equals(_selectionQuote, quote, StringComparison.Ordinal))
            return;

        _selectionAutoSaveCts?.Cancel();
        _selectionAutoSaveCts?.Dispose();
        _selectionAutoSaveCts = null;
        _selectionQuote = quote;

        var existing = selectedAnnotation ?? (_book is null
            ? null
            : App.ReadingData.GetAnnotations(_book.Id)
                .LastOrDefault(item => item.Locator.SectionIndex == _sectionIndex
                                       && string.Equals(item.Quote.Trim(), quote, StringComparison.Ordinal)
                                       && !string.IsNullOrWhiteSpace(item.Note)));
        _selectionAnnotationId = existing?.Id;
        _selectionTextInternalUpdate = true;
        try
        {
            SelectionAnnotationTextBox.Text = existing?.Note ?? string.Empty;
        }
        finally
        {
            _selectionTextInternalUpdate = false;
        }

        var origin = ReaderWebView.TransformToVisual(ReaderRootGrid).TransformPoint(new Point(0, 0));
        const double popupWidth = 404;
        const double popupHeight = 102;
        var maxX = Math.Max(12, ReaderRootGrid.ActualWidth - popupWidth - 12);
        var x = Math.Clamp(origin.X + payload.X + payload.Width / 2 - popupWidth / 2, 12, maxX);
        var below = origin.Y + payload.Y + payload.Height + 10;
        var maxY = Math.Max(12, ReaderRootGrid.ActualHeight - popupHeight - 12);
        var y = below <= maxY ? below : Math.Max(12, origin.Y + payload.Y - popupHeight - 10);

        SelectionAnnotationPopup.HorizontalOffset = x;
        SelectionAnnotationPopup.VerticalOffset = y;
        SelectionAnnotationPopup.IsOpen = true;
        SelectionAnnotationTextBox.Focus(FocusState.Programmatic);
    }

    private void SelectionAnnotationTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_selectionTextInternalUpdate || string.IsNullOrWhiteSpace(_selectionQuote)) return;

        _selectionAutoSaveCts?.Cancel();
        _selectionAutoSaveCts?.Dispose();
        _selectionAutoSaveCts = new CancellationTokenSource();
        _ = DebouncedSelectionAutoSaveAsync(_selectionAutoSaveCts.Token);
    }

    private async Task DebouncedSelectionAutoSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(400, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await PersistSelectionNoteAsync(refreshHighlight: false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Selection note autosave failed", ex);
        }
    }

    private async Task FlushSelectionNoteAsync(bool refreshHighlight)
    {
        _selectionAutoSaveCts?.Cancel();
        _selectionAutoSaveCts?.Dispose();
        _selectionAutoSaveCts = null;
        await PersistSelectionNoteAsync(refreshHighlight);
    }

    private async Task PersistSelectionNoteAsync(bool refreshHighlight)
    {
        if (_book is null || _document is null || string.IsNullOrWhiteSpace(_selectionQuote)) return;

        var note = SelectionAnnotationTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            if (!string.IsNullOrWhiteSpace(_selectionAnnotationId))
            {
                App.ReadingData.RemoveAnnotation(_selectionAnnotationId);
                _selectionAnnotationId = null;
                RefreshAnnotations();
                FilterAnnotationItemsToNotes();
                if (refreshHighlight) await ApplyNoteOnlyHighlightsAsync();
            }
            return;
        }

        var chapterTitle = FlowSearchService.ResolveChapterTitle(_document, _sectionIndex);
        var saved = App.ReadingData.SaveAnnotation(new ReaderAnnotation
        {
            Id = _selectionAnnotationId ?? Guid.NewGuid().ToString("N"),
            BookId = _book.Id,
            Locator = new FlowContentLocator(_sectionIndex, _sectionFraction, TextQuote: _selectionQuote),
            ChapterTitle = chapterTitle,
            Quote = _selectionQuote,
            Note = note,
            HighlightColor = "note-red"
        });
        _selectionAnnotationId = saved.Id;
        RefreshAnnotations();
        FilterAnnotationItemsToNotes();
        if (refreshHighlight) await ApplyNoteOnlyHighlightsAsync();
    }

    private async Task CloseSelectionPopupAsync(bool saveBeforeClose)
    {
        if (!SelectionAnnotationPopup.IsOpen && string.IsNullOrWhiteSpace(_selectionQuote)) return;
        if (_selectionPopupClosing) return;

        _selectionPopupClosing = true;
        try
        {
            if (saveBeforeClose) await FlushSelectionNoteAsync(refreshHighlight: true);
            SelectionAnnotationPopup.IsOpen = false;
            ClearSelectionPopupState();
        }
        finally
        {
            _selectionPopupClosing = false;
        }
    }

    private void ClearSelectionPopupState()
    {
        _selectionAutoSaveCts?.Cancel();
        _selectionAutoSaveCts?.Dispose();
        _selectionAutoSaveCts = null;
        _selectionQuote = string.Empty;
        _selectionAnnotationId = null;
        _selectionTextInternalUpdate = true;
        try
        {
            SelectionAnnotationTextBox.Text = string.Empty;
        }
        finally
        {
            _selectionTextInternalUpdate = false;
        }
    }

}
