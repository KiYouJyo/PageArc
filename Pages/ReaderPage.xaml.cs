using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using PageArc.Models;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class ReaderPage : Page
{
    private BookEntry? _book;
    private EpubDocument? _document;
    private EpubRenderChapter? _currentChapter;
    private int _spineIndex;
    private bool _settingsReady;

    public ReaderPage()
    {
        StartupDiagnostics.Log("ReaderPage constructor entered.");
        try
        {
            InitializeComponent();
            StartupDiagnostics.Log("ReaderPage.InitializeComponent completed.");
            Loaded += ReaderPage_Loaded;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("ReaderPage constructor failed", ex);
            throw;
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _book = e.Parameter as BookEntry;
        StartupDiagnostics.Log($"ReaderPage.OnNavigatedTo: book={_book?.FilePath ?? "<null>"}.");
    }

    private async void ReaderPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_book is null)
        {
            ShowReaderError("The selected ebook could not be passed to the reader.");
            return;
        }

        StartupDiagnostics.Log($"Reader opening '{_book.FilePath}' ({_book.Format}).");
        BookTitleText.Text = _book.Title;
        App.Library.MarkOpened(_book);
        SelectByTag(ReaderThemeCombo, App.Settings.Current.ReadingTheme);
        ReaderFontScaleSlider.Value = App.Settings.Current.FontScale;
        ReaderLineHeightSlider.Value = App.Settings.Current.LineHeight;
        ContinuousScrollToggle.IsOn = App.Settings.Current.ContinuousScrolling;
        _settingsReady = true;

        if (!_book.Format.Equals("EPUB", StringComparison.OrdinalIgnoreCase))
        {
            ReaderInfoBar.Severity = InfoBarSeverity.Warning;
            ReaderInfoBar.Message = App.Localization.GetString("Reader_UnsupportedV01");
            ReaderInfoBar.IsOpen = true;
            return;
        }

        ReaderLoadingLayer.Visibility = Visibility.Visible;
        try
        {
            _document = await EpubParser.OpenAsync(_book);
            if (_document.Spine.Count == 0)
                throw new InvalidDataException("This EPUB does not contain any readable spine items.");

            StartupDiagnostics.Log($"EPUB parsed: {_document.Spine.Count} spine items, {_document.Toc.Count} TOC entries.");
            TocList.ItemsSource = _document.Toc.Count > 0
                ? _document.Toc.Select(x => x.Title).ToList()
                : _document.Spine.Select((_, i) => string.Format(App.Localization.GetString("Reader_ChapterN"), i + 1)).ToList();
            ContentsMetaText.Text = $"{_document.Spine.Count} · EPUB";

            var requestedIndex = EpubWebRenderer.ResolveInitialSpineIndex(_document, _book.SpineIndex, _book.Progress);
            await NavigateToSpineAsync(requestedIndex, preferReadableText: true);
        }
        catch (Exception ex)
        {
            ReaderLoadingLayer.Visibility = Visibility.Collapsed;
            StartupDiagnostics.Log("EPUB reader initialization failed", ex);
            ShowReaderError(ex.Message);
        }
    }

    private static void SelectByTag(ComboBox comboBox, string tag)
    {
        comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(x.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            ?? comboBox.Items.FirstOrDefault();
    }

    private async Task NavigateToSpineAsync(int index, bool preferReadableText = false)
    {
        if (_document is null || _book is null) return;

        ReaderLoadingLayer.Visibility = Visibility.Visible;
        ReaderInfoBar.IsOpen = false;
        try
        {
            var targetIndex = Math.Clamp(index, 0, _document.Spine.Count - 1);
            var chapter = await EpubWebRenderer.PrepareAsync(_document, targetIndex);

            if (preferReadableText && string.IsNullOrWhiteSpace(chapter.PlainText))
            {
                var readable = await FindReadableChapterAsync(targetIndex);
                if (readable is not null)
                {
                    targetIndex = readable.Value.Index;
                    chapter = readable.Value.Chapter;
                }
            }

            _spineIndex = targetIndex;
            _currentChapter = chapter;
            NativeReaderText.Text = chapter.PlainText;
            NativeReaderScroll.ChangeView(null, 0, null, true);

            if (string.IsNullOrWhiteSpace(chapter.PlainText))
            {
                ReaderInfoBar.Severity = InfoBarSeverity.Informational;
                ReaderInfoBar.Message = "This page contains no text. Use the previous or next chapter button to continue.";
                ReaderInfoBar.IsOpen = true;
            }

            _book.SpineIndex = _spineIndex;
            _book.Progress = (_spineIndex + 1d) / _document.Spine.Count;
            App.Library.Save();

            ReaderProgress.Maximum = _document.Spine.Count;
            ReaderProgress.Value = _spineIndex + 1;
            ChapterProgressText.Text = string.Format(App.Localization.GetString("Reader_ChapterN"), _spineIndex + 1);
            ReaderPercentText.Text = $"{Math.Round(_book.Progress * 100)}%";
            BookProgressText.Text = string.Format(App.Localization.GetString("Reader_ReadPercent"), Math.Round(_book.Progress * 100));
            ApplyNativeReaderStyle();
            StartupDiagnostics.Log($"EPUB native render succeeded for spine {_spineIndex}: {_document.Spine[_spineIndex].RelativePath}, chars={chapter.PlainText.Length}.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"EPUB native render failed for spine {index}", ex);
            ShowReaderError(ex.Message);
        }
        finally
        {
            ReaderLoadingLayer.Visibility = Visibility.Collapsed;
        }
    }

    private async Task<(int Index, EpubRenderChapter Chapter)?> FindReadableChapterAsync(int requestedIndex)
    {
        if (_document is null) return null;

        // Prefer subsequent content because EPUBs often start with cover/title pages.
        for (var i = requestedIndex + 1; i < _document.Spine.Count; i++)
        {
            var candidate = await EpubWebRenderer.PrepareAsync(_document, i);
            if (!string.IsNullOrWhiteSpace(candidate.PlainText)) return (i, candidate);
        }

        for (var i = requestedIndex - 1; i >= 0; i--)
        {
            var candidate = await EpubWebRenderer.PrepareAsync(_document, i);
            if (!string.IsNullOrWhiteSpace(candidate.PlainText)) return (i, candidate);
        }

        return null;
    }

    private void ApplyNativeReaderStyle()
    {
        if (!_settingsReady) return;
        var settings = App.Settings.Current;
        var fontSize = 18d * settings.FontScale;
        NativeReaderText.FontSize = fontSize;
        NativeReaderText.LineHeight = Math.Max(fontSize + 4, fontSize * settings.LineHeight);
        NativeReaderText.FontFamily = settings.DefaultFont switch
        {
            "Segoe UI Variable" => new FontFamily("Segoe UI Variable"),
            "Georgia" => new FontFamily("Georgia"),
            _ => new FontFamily("Segoe UI Variable")
        };

        NativeReaderText.MaxWidth = settings.PageWidth switch
        {
            "narrow" => 560,
            "wide" => 880,
            _ => 700
        };
        ReaderSurface.MaxWidth = NativeReaderText.MaxWidth + 140;

        var (background, foreground) = settings.ReadingTheme switch
        {
            "dark" => (ColorHelper.FromArgb(255, 35, 42, 42), ColorHelper.FromArgb(255, 242, 246, 246)),
            "sepia" => (ColorHelper.FromArgb(255, 244, 234, 211), ColorHelper.FromArgb(255, 68, 58, 46)),
            _ => (ColorHelper.FromArgb(255, 247, 251, 251), ColorHelper.FromArgb(255, 23, 37, 38))
        };
        ReaderSurface.Background = new SolidColorBrush(background);
        NativeReaderText.Foreground = new SolidColorBrush(foreground);
    }

    private void ShowReaderError(string message)
    {
        ReaderInfoBar.Severity = InfoBarSeverity.Error;
        ReaderInfoBar.Message = message;
        ReaderInfoBar.IsOpen = true;
    }

    private void Back_Click(object sender, RoutedEventArgs e) => App.MainWindow?.ExitReader();

    private void Contents_Click(object sender, RoutedEventArgs e) =>
        ContentsColumn.Width = ContentsColumn.Width.Value > 0 ? new GridLength(0) : new GridLength(260);

    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (_document is not null && _spineIndex > 0)
            await NavigateToSpineAsync(_spineIndex - 1);
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_document is not null && _spineIndex < _document.Spine.Count - 1)
            await NavigateToSpineAsync(_spineIndex + 1);
    }

    private async void TocList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_document is null || TocList.SelectedIndex < 0) return;
        if (_document.Toc.Count == 0)
        {
            await NavigateToSpineAsync(TocList.SelectedIndex, preferReadableText: true);
            return;
        }

        var tocHref = _document.Toc[TocList.SelectedIndex].Href;
        var normalizedHref = EpubPath.Normalize(tocHref);
        var index = _document.Spine.ToList().FindIndex(x =>
            string.Equals(EpubPath.Normalize(x.RelativePath), normalizedHref, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) await NavigateToSpineAsync(index, preferReadableText: true);
    }

    private async void ReaderThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => await SaveAndApplyReaderSettingsAsync();
    private async void ReaderFontScaleSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => await SaveAndApplyReaderSettingsAsync();
    private async void ReaderLineHeightSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => await SaveAndApplyReaderSettingsAsync();
    private async void ContinuousScrollToggle_Toggled(object sender, RoutedEventArgs e) => await SaveAndApplyReaderSettingsAsync();

    private Task SaveAndApplyReaderSettingsAsync()
    {
        if (!_settingsReady) return Task.CompletedTask;
        App.Settings.Update(settings =>
        {
            if (ReaderThemeCombo.SelectedItem is ComboBoxItem { Tag: string theme }) settings.ReadingTheme = theme;
            settings.FontScale = ReaderFontScaleSlider.Value;
            settings.LineHeight = ReaderLineHeightSlider.Value;
            settings.ContinuousScrolling = ContinuousScrollToggle.IsOn;
        });
        ApplyNativeReaderStyle();
        return Task.CompletedTask;
    }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        ReaderInfoBar.Severity = InfoBarSeverity.Success;
        ReaderInfoBar.Message = App.Localization.GetString("Reader_BookmarkSaved");
        ReaderInfoBar.IsOpen = true;
    }
}
