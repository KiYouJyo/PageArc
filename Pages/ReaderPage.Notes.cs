using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class ReaderPage
{
    private readonly ObservableCollection<ReaderAnnotationListItem> _annotationItems = [];
    private bool _notesInitialized;
    private bool _annotationNavigationHooked;
    private WebView2? _kindleParserWebView;
    private WebViewKindleParserRuntime? _kindleParserRuntime;

    private void ReaderPage_NotesLoaded(object sender, RoutedEventArgs e)
    {
        ConfigureKindleFlowRuntime();
        SetupAnnotationNavigationHook();
        ApplyProgressVisibility();

        if (_notesInitialized) return;
        _notesInitialized = true;
        NotesList.ItemsSource = _annotationItems;

        ContentsButton.Click += (_, _) => NotesMode.Visibility = Visibility.Collapsed;
        SearchButton.Click += (_, _) => NotesMode.Visibility = Visibility.Collapsed;
        BookmarkButton.Click += (_, _) => NotesMode.Visibility = Visibility.Collapsed;
        RefreshAnnotations();
    }

    private void ConfigureKindleFlowRuntime()
    {
        if (_kindleParserRuntime is not null || _book is null) return;
        var format = BookFormatRegistry.Normalize(_book.Format);
        if (string.IsNullOrWhiteSpace(format)) format = BookFormatRegistry.FormatFromPath(_book.FilePath);
        if (format is not ("MOBI" or "AZW3")) return;
        if (Content is not Grid root) return;

        _kindleParserWebView = new WebView2
        {
            Width = 1,
            Height = 1,
            Opacity = 0,
            IsHitTestVisible = false,
            IsTabStop = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        Grid.SetRowSpan(_kindleParserWebView, 2);
        root.Children.Add(_kindleParserWebView);

        _kindleParserRuntime = new WebViewKindleParserRuntime(_kindleParserWebView);
        _readerEngine.RegisterAdapter(new MobiFlowAdapter(_kindleParserRuntime), prefer: true);
    }

    private void SetupAnnotationNavigationHook()
    {
        if (_annotationNavigationHooked) return;
        _annotationNavigationHooked = true;
        ReaderWebView.NavigationCompleted += async (_, args) =>
        {
            if (args.IsSuccess) await ApplyCurrentAnnotationsAsync();
        };
    }

    private void ApplyProgressVisibility()
    {
        ReaderProgress.Visibility = Visibility.Visible;
        ChapterProgressText.Visibility = Visibility.Visible;
        ReaderPercentText.Visibility = Visibility.Visible;
        BookProgressText.Visibility = Visibility.Visible;
    }

    private async void AddHighlight_Click(object sender, RoutedEventArgs e)
    {
        var quote = await GetSelectedTextAsync();
        if (string.IsNullOrWhiteSpace(quote))
        {
            ShowSelectionRequired();
            return;
        }
        await SaveSelectedAnnotationAsync(quote, null, "yellow");
    }

    private async void AddNote_Click(object sender, RoutedEventArgs e)
    {
        var quote = await GetSelectedTextAsync();
        if (string.IsNullOrWhiteSpace(quote))
        {
            ShowSelectionRequired();
            return;
        }

        var input = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 96,
            MaxLength = 2000,
            PlaceholderText = ReaderText("输入笔记…", "ノートを入力…", "Write a note…")
        };
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = quote,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 4,
            Opacity = 0.72
        });
        content.Children.Add(input);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = ReaderText("添加笔记", "ノートを追加", "Add note"),
            Content = content,
            PrimaryButtonText = ReaderText("保存", "保存", "Save"),
            CloseButtonText = ReaderText("取消", "キャンセル", "Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await SaveSelectedAnnotationAsync(quote, input.Text.Trim(), "blue");
    }

    private async Task<string> GetSelectedTextAsync()
    {
        if (!_webReady || ReaderWebView.CoreWebView2 is null) return string.Empty;
        try
        {
            var json = await ReaderWebView.CoreWebView2.ExecuteScriptAsync("(window.getSelection ? window.getSelection().toString() : '')");
            var text = JsonSerializer.Deserialize<string>(json)?.Trim() ?? string.Empty;
            return text.Length <= 2000 ? text : text[..2000];
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Reader selection capture failed", ex);
            return string.Empty;
        }
    }

    private async Task SaveSelectedAnnotationAsync(string quote, string? note, string color)
    {
        if (_book is null || _document is null) return;
        var chapterTitle = FlowSearchService.ResolveChapterTitle(_document, _sectionIndex);
        App.ReadingData.SaveAnnotation(new ReaderAnnotation
        {
            BookId = _book.Id,
            Locator = new FlowContentLocator(_sectionIndex, _sectionFraction, TextQuote: quote),
            ChapterTitle = chapterTitle,
            Quote = quote,
            Note = string.IsNullOrWhiteSpace(note) ? null : note,
            HighlightColor = color
        });
        RefreshAnnotations();
        await ApplyCurrentAnnotationsAsync();

        ReaderInfoBar.Severity = InfoBarSeverity.Success;
        ReaderInfoBar.Message = string.IsNullOrWhiteSpace(note)
            ? ReaderText("已保存高亮。", "ハイライトを保存しました。", "Highlight saved.")
            : ReaderText("已保存笔记。", "ノートを保存しました。", "Note saved.");
        ReaderInfoBar.IsOpen = true;
    }

    private void ShowSelectionRequired()
    {
        ReaderInfoBar.Severity = InfoBarSeverity.Informational;
        ReaderInfoBar.Message = ReaderText(
            "请先在正文中选择文字。",
            "先に本文のテキストを選択してください。",
            "Select text in the book first.");
        ReaderInfoBar.IsOpen = true;
    }

    private async Task ApplyCurrentAnnotationsAsync()
    {
        if (!_webReady || ReaderWebView.CoreWebView2 is null || _book is null) return;
        var annotations = App.ReadingData.GetAnnotations(_book.Id)
            .Where(item => item.Locator.SectionIndex == _sectionIndex && !string.IsNullOrWhiteSpace(item.Quote))
            .Select(item => new
            {
                id = item.Id,
                quote = item.Quote,
                color = item.HighlightColor.ToLowerInvariant()
            })
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
              const palette = {
                yellow: 'rgba(250,194,46,.34)',
                blue: 'rgba(107,184,235,.30)',
                green: 'rgba(140,199,128,.30)'
              };
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
                  mark.style.background = palette[item.color] || palette.yellow;
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
            StartupDiagnostics.Log("Reader annotation rendering failed", ex);
        }
    }

    private async Task ScrollToAnnotationAsync(string annotationId)
    {
        if (!_webReady || ReaderWebView.CoreWebView2 is null) return;
        var idJson = JsonSerializer.Serialize(annotationId);
        var script = """
            (() => {
              const id = __ID__;
              const mark = Array.from(document.querySelectorAll('mark.pagearc-annotation'))
                .find(item => item.dataset.pagearcAnnotationId === id);
              if (!mark) return false;
              mark.scrollIntoView({block:'center', inline:'center', behavior:'smooth'});
              return true;
            })()
            """.Replace("__ID__", idJson, StringComparison.Ordinal);
        try { await ReaderWebView.CoreWebView2.ExecuteScriptAsync(script); }
        catch { }
    }

    private void Notes_Click(object sender, RoutedEventArgs e)
    {
        _sidebarMode = ReaderSidebarMode.Bookmarks;
        ContentsColumn.Width = new GridLength(260);
        ContentsMode.Visibility = Visibility.Collapsed;
        SearchMode.Visibility = Visibility.Collapsed;
        BookmarksMode.Visibility = Visibility.Collapsed;
        NotesMode.Visibility = Visibility.Visible;
        RefreshAnnotations();
    }

    private void RefreshAnnotations()
    {
        _annotationItems.Clear();
        var annotations = _book is null
            ? Array.Empty<ReaderAnnotation>()
            : App.ReadingData.GetAnnotations(_book.Id).ToArray();

        foreach (var annotation in annotations)
            _annotationItems.Add(new ReaderAnnotationListItem { Annotation = annotation });

        var noteCount = annotations.Count(item => !string.IsNullOrWhiteSpace(item.Note));
        var language = App.Localization.CurrentLanguage;
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            NotesHeading.Text = "笔记";
            NotesMetaText.Text = $"{annotations.Length} 处高亮 · {noteCount} 条笔记";
            NotesFooterText.Text = $"{annotations.Length} 条标注";
        }
        else if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            NotesHeading.Text = "ノート";
            NotesMetaText.Text = $"ハイライト {annotations.Length} 件 · ノート {noteCount} 件";
            NotesFooterText.Text = $"注釈 {annotations.Length} 件";
        }
        else
        {
            NotesHeading.Text = "Notes";
            NotesMetaText.Text = $"{annotations.Length} highlights · {noteCount} notes";
            NotesFooterText.Text = $"{annotations.Length} annotations";
        }
    }

    private async void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NotesList.SelectedItem is not ReaderAnnotationListItem item) return;
        NotesList.SelectedItem = null;
        _sectionFraction = item.Annotation.Locator.Fraction;
        await NavigateToSectionAsync(item.Annotation.Locator.SectionIndex, restoreSavedFraction: true);
        await ApplyCurrentAnnotationsAsync();
        await ScrollToAnnotationAsync(item.Annotation.Id);
    }

    private static string ReaderText(string zh, string ja, string en)
    {
        var language = App.Localization.CurrentLanguage;
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return zh;
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return ja;
        return en;
    }
}
