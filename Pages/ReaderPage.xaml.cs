using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using PageArc.Models;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class ReaderPage : Page
{
    private BookEntry? _book;
    private EpubDocument? _document;
    private int _spineIndex;
    private bool _settingsReady;

    public ReaderPage()
    {
        InitializeComponent();
        Loaded += ReaderPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _book = e.Parameter as BookEntry;
    }

    private async void ReaderPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_book is null) return;
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

        try
        {
            _document = await EpubParser.OpenAsync(_book);
            _spineIndex = Math.Clamp(_book.SpineIndex, 0, _document.Spine.Count - 1);
            await BookWebView.EnsureCoreWebView2Async();
            BookWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("pagearc.local", _document.ExtractionRoot, CoreWebView2HostResourceAccessKind.Allow);
            TocList.ItemsSource = _document.Toc.Count > 0
                ? _document.Toc.Select(x => x.Title).ToList()
                : _document.Spine.Select((_, i) => string.Format(App.Localization.GetString("Reader_ChapterN"), i + 1)).ToList();
            ContentsMetaText.Text = $"{_document.Spine.Count} · EPUB";
            NavigateToSpine(_spineIndex);
        }
        catch (Exception ex)
        {
            ReaderInfoBar.Severity = InfoBarSeverity.Error;
            ReaderInfoBar.Message = ex.Message;
            ReaderInfoBar.IsOpen = true;
        }
    }

    private static void SelectByTag(ComboBox comboBox, string tag)
    {
        comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(x.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            ?? comboBox.Items.FirstOrDefault();
    }

    private void NavigateToSpine(int index)
    {
        if (_document is null || _book is null) return;
        _spineIndex = Math.Clamp(index, 0, _document.Spine.Count - 1);
        var relative = _document.Spine[_spineIndex].RelativePath.Split('/').Select(Uri.EscapeDataString);
        BookWebView.Source = new Uri($"https://pagearc.local/{string.Join('/', relative)}");
        _book.SpineIndex = _spineIndex;
        _book.Progress = (_spineIndex + 1d) / _document.Spine.Count;
        App.Library.Save();
        ReaderProgress.Maximum = _document.Spine.Count;
        ReaderProgress.Value = _spineIndex + 1;
        ChapterProgressText.Text = string.Format(App.Localization.GetString("Reader_ChapterN"), _spineIndex + 1);
        ReaderPercentText.Text = $"{Math.Round(_book.Progress * 100)}%";
        BookProgressText.Text = string.Format(App.Localization.GetString("Reader_ReadPercent"), Math.Round(_book.Progress * 100));
    }

    private async void BookWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (args.IsSuccess) await ApplyReaderStyleAsync();
    }

    private async Task ApplyReaderStyleAsync()
    {
        if (BookWebView.CoreWebView2 is null) return;
        var settings = App.Settings.Current;
        var colors = settings.ReadingTheme switch
        {
            "dark" => (Background: "#1f1f1f", Foreground: "#f2f2f2"),
            "sepia" => (Background: "#f4ead3", Foreground: "#443a2e"),
            _ => (Background: "#ffffff", Foreground: "#202020")
        };
        var maxWidth = settings.PageWidth switch { "narrow" => "34rem", "wide" => "52rem", _ => "42rem" };
        var font = settings.DefaultFont == "book" ? "inherit" : JsonSerializer.Serialize(settings.DefaultFont);
        var scale = settings.FontScale.ToString(CultureInfo.InvariantCulture);
        var lineHeight = settings.LineHeight.ToString(CultureInfo.InvariantCulture);
        var script = $$"""
        (() => {
          const old = document.getElementById('pagearc-reader-style');
          if (old) old.remove();
          const s = document.createElement('style');
          s.id = 'pagearc-reader-style';
          s.textContent = `html{background:{{colors.Background}}!important}body{background:{{colors.Background}}!important;color:{{colors.Foreground}}!important;max-width:{{maxWidth}};margin:0 auto!important;padding:3.5rem 4.5rem 5rem!important;font-size:{{scale}}em!important;line-height:{{lineHeight}}!important;font-family:{{font}}!important}img,svg{max-width:100%;height:auto}`;
          document.head.appendChild(s);
        })();
        """;
        await BookWebView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private void Back_Click(object sender, RoutedEventArgs e) => App.MainWindow?.ExitReader();
    private void Contents_Click(object sender, RoutedEventArgs e) => ContentsColumn.Width = ContentsColumn.Width.Value > 0 ? new GridLength(0) : new GridLength(260);
    private void Previous_Click(object sender, RoutedEventArgs e) { if (_document is not null && _spineIndex > 0) NavigateToSpine(_spineIndex - 1); }
    private void Next_Click(object sender, RoutedEventArgs e) { if (_document is not null && _spineIndex < _document.Spine.Count - 1) NavigateToSpine(_spineIndex + 1); }

    private void TocList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_document is null || TocList.SelectedIndex < 0) return;
        if (_document.Toc.Count == 0) { NavigateToSpine(TocList.SelectedIndex); return; }
        var href = _document.Toc[TocList.SelectedIndex].Href.Split('#')[0];
        var index = _document.Spine.ToList().FindIndex(x => string.Equals(x.RelativePath, href, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) NavigateToSpine(index);
    }

    private async void ReaderThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => await SaveAndApplyReaderSettingsAsync();
    private async void ReaderFontScaleSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => await SaveAndApplyReaderSettingsAsync();
    private async void ReaderLineHeightSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => await SaveAndApplyReaderSettingsAsync();
    private async void ContinuousScrollToggle_Toggled(object sender, RoutedEventArgs e) => await SaveAndApplyReaderSettingsAsync();

    private async Task SaveAndApplyReaderSettingsAsync()
    {
        if (!_settingsReady) return;
        App.Settings.Update(settings =>
        {
            if (ReaderThemeCombo.SelectedItem is ComboBoxItem { Tag: string theme }) settings.ReadingTheme = theme;
            settings.FontScale = ReaderFontScaleSlider.Value;
            settings.LineHeight = ReaderLineHeightSlider.Value;
            settings.ContinuousScrolling = ContinuousScrollToggle.IsOn;
        });
        await ApplyReaderStyleAsync();
    }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        ReaderInfoBar.Severity = InfoBarSeverity.Success;
        ReaderInfoBar.Message = App.Localization.GetString("Reader_BookmarkSaved");
        ReaderInfoBar.IsOpen = true;
    }
}
