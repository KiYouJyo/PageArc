using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    }

    private bool _selectionAnnotationUiReady;
    private bool _selectionMessageHooked;
    private string _selectionQuote = string.Empty;

    private void InitializeSelectionAnnotationUi()
    {
        if (_selectionAnnotationUiReady) return;
        _selectionAnnotationUiReady = true;

        // v0.9.3 refined contract: notes are created directly from a text selection.
        // Custom highlight colors are intentionally deferred; saved notes use one muted-red mark.
        MoreButton.Flyout = null;
        AnnotationHighlightLabel.Visibility = Visibility.Collapsed;
        HighlightYellowButton.Visibility = Visibility.Collapsed;
        HighlightBlueButton.Visibility = Visibility.Collapsed;
        HighlightGreenButton.Visibility = Visibility.Collapsed;
        AnnotationHintText.Visibility = Visibility.Collapsed;
        SelectionAnnotationTextBox.PlaceholderText = ReaderText("为所选文字添加笔记…", "選択したテキストにノートを追加…", "Add a note to the selection…");
        SaveSelectionAnnotationButton.Content = ReaderText("保存", "保存", "Save");
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
              document.addEventListener('mouseup', () => setTimeout(report, 0), true);
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

    private void HandleSelectionWebMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (string.Equals(message, "selection-clear", StringComparison.Ordinal))
        {
            SelectionAnnotationPopup.IsOpen = false;
            _selectionQuote = string.Empty;
            return;
        }
        if (!message.StartsWith("selection:", StringComparison.Ordinal)) return;

        try
        {
            var payload = JsonSerializer.Deserialize<SelectionPopupPayload>(message["selection:".Length..]);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Text)) return;
            ShowSelectionAnnotationPopup(payload);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Reader selection message parsing failed", ex);
        }
    }

    private void ShowSelectionAnnotationPopup(SelectionPopupPayload payload)
    {
        _selectionQuote = payload.Text.Trim();
        SelectionAnnotationTextBox.Text = string.Empty;

        var origin = ReaderWebView.TransformToVisual(ReaderRootGrid).TransformPoint(new Point(0, 0));
        const double popupWidth = 404;
        const double popupHeight = 168;
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

    // Retained only because the pre-refinement XAML still has the old click bindings.
    // The controls are collapsed and this method is intentionally inert.
    private void AnnotationColor_Click(object sender, RoutedEventArgs e)
    {
    }

    private async void SaveSelectionAnnotation_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectionQuote)) return;
        var note = SelectionAnnotationTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            ReaderInfoBar.Severity = InfoBarSeverity.Informational;
            ReaderInfoBar.Message = ReaderText("请输入笔记内容。", "ノートを入力してください。", "Write a note before saving.");
            ReaderInfoBar.IsOpen = true;
            return;
        }

        await SaveSelectedAnnotationAsync(_selectionQuote, note, "note-red");
        FilterAnnotationItemsToNotes();
        await ApplyNoteOnlyHighlightsAsync();
        SelectionAnnotationPopup.IsOpen = false;
        if (ReaderWebView.CoreWebView2 is not null)
        {
            try { await ReaderWebView.CoreWebView2.ExecuteScriptAsync("window.getSelection?.().removeAllRanges?.(); true"); }
            catch { }
        }
        _selectionQuote = string.Empty;
    }

    private void AnnotationPopupClose_Click(object sender, RoutedEventArgs e)
    {
        SelectionAnnotationPopup.IsOpen = false;
        _selectionQuote = string.Empty;
    }
}
