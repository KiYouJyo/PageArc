using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
    private string _selectionAnnotationColor = "yellow";

    private void InitializeSelectionAnnotationUi()
    {
        if (_selectionAnnotationUiReady) return;
        _selectionAnnotationUiReady = true;

        // Notes/highlights are created from a text selection now, not from the top ••• menu.
        MoreButton.Flyout = null;
        AnnotationHighlightLabel.Text = ReaderText("高亮", "ハイライト", "Highlight");
        SelectionAnnotationTextBox.PlaceholderText = ReaderText("为所选文字添加笔记…", "選択したテキストにノートを追加…", "Add a note to the selection…");
        AnnotationHintText.Text = ReaderText("选择高亮颜色并直接输入批注", "色を選び、必要ならノートを入力", "Choose a highlight color and optionally add a note");
        SaveSelectionAnnotationButton.Content = ReaderText("保存", "保存", "Save");
        UpdateSelectionAnnotationColorVisuals();

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
        _selectionAnnotationColor = "yellow";
        SelectionAnnotationTextBox.Text = string.Empty;
        UpdateSelectionAnnotationColorVisuals();

        var origin = ReaderWebView.TransformToVisual(ReaderRootGrid).TransformPoint(new Point(0, 0));
        const double popupWidth = 404;
        const double popupHeight = 174;
        var maxX = Math.Max(12, ReaderRootGrid.ActualWidth - popupWidth - 12);
        var x = Math.Clamp(origin.X + payload.X + payload.Width / 2 - popupWidth / 2, 12, maxX);
        var below = origin.Y + payload.Y + payload.Height + 10;
        var maxY = Math.Max(12, ReaderRootGrid.ActualHeight - popupHeight - 12);
        var y = below <= maxY ? below : Math.Max(12, origin.Y + payload.Y - popupHeight - 10);

        SelectionAnnotationPopup.HorizontalOffset = x;
        SelectionAnnotationPopup.VerticalOffset = y;
        SelectionAnnotationPopup.IsOpen = true;
    }

    private void AnnotationColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string color }) return;
        _selectionAnnotationColor = color is "blue" or "green" ? color : "yellow";
        UpdateSelectionAnnotationColorVisuals();
    }

    private void UpdateSelectionAnnotationColorVisuals()
    {
        SetAnnotationColorState(HighlightYellowButton, "yellow", ColorHelper.FromArgb(255, 250, 194, 46));
        SetAnnotationColorState(HighlightBlueButton, "blue", ColorHelper.FromArgb(255, 107, 184, 235));
        SetAnnotationColorState(HighlightGreenButton, "green", ColorHelper.FromArgb(255, 140, 199, 128));
    }

    private void SetAnnotationColorState(Button button, string color, Windows.UI.Color accent)
    {
        var selected = string.Equals(_selectionAnnotationColor, color, StringComparison.Ordinal);
        button.BorderBrush = new SolidColorBrush(accent);
        button.BorderThickness = new Thickness(selected ? 2 : 1);
        button.Background = new SolidColorBrush(ColorHelper.FromArgb(selected ? (byte)42 : (byte)18, accent.R, accent.G, accent.B));
    }

    private async void SaveSelectionAnnotation_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectionQuote)) return;
        var note = SelectionAnnotationTextBox.Text.Trim();
        await SaveSelectedAnnotationAsync(_selectionQuote, string.IsNullOrWhiteSpace(note) ? null : note, _selectionAnnotationColor);
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
