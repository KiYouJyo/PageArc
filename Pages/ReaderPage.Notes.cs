using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class ReaderPage
{
    private readonly ObservableCollection<ReaderAnnotationListItem> _annotationItems = [];
    private bool _notesInitialized;
    private WebView2? _kindleParserWebView;
    private WebViewKindleParserRuntime? _kindleParserRuntime;

    private void ReaderPage_NotesLoaded(object sender, RoutedEventArgs e)
    {
        ConfigureKindleFlowRuntime();
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
    }
}
