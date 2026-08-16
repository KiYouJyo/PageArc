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
    private bool _virtualHostReady;
    private string? _currentRenderHtml;

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

        try
        {
            _document = await EpubParser.OpenAsync(_book);
            StartupDiagnostics.Log($"EPUB parsed: {_document.Spine.Count} spine items, {_document.Toc.Count} TOC entries.");
            _spineIndex = Math.Clamp(_book.SpineIndex, 0, _document.Spine.Count - 1);

            await BookWebView.EnsureCoreWebView2Async();
            try
            {
                BookWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "pagearc.local",
                    _document.ExtractionRoot,
                    CoreWebView2HostResourceAccessKind.Allow);
                _virtualHostReady = true;
                StartupDiagnostics.Log("EPUB WebView virtual host mapping ready for chapter resources.");
            }
            catch (Exception mappingException)
            {
                // Chapter text is deliberately loaded with NavigateToString, so a mapping
                // failure must not make the book unreadable. Only images/CSS may be absent.
                _virtualHostReady = false;
                StartupDiagnostics.Log("EPUB resource mapping failed; continuing with text-only chapter rendering", mappingException);
            }

            TocList.ItemsSource = _document.Toc.Count > 0
                ? _document.Toc.Select(x => x.Title).ToList()
                : _document.Spine.Select((_, i) => string.Format(App.Localization.GetString("Reader_ChapterN"), i + 1)).ToList();
            ContentsMetaText.Text = $"{_document.Spine.Count} · EPUB";
            await NavigateToSpineAsync(_spineIndex);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("EPUB reader initialization failed", ex);
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

    private async Task NavigateToSpineAsync(int index, string? fragment = null)
    {
        if (_document is null || _book is null) return;
        _spineIndex = Math.Clamp(index, 0, _document.Spine.Count - 1);

        var rendered = await EpubWebRenderer.PrepareAsync(_document, _spineIndex);
        _currentRenderHtml = rendered.Html;
        var fragmentValue = NormalizeFragmentValue(fragment);

        // Do not navigate the top-level WebView to a virtual-host file. A number of valid
        // EPUB2 books (including Calibre-generated books with .html spine items and NCX)
        // can report a successful host navigation while Chromium renders an empty surface.
        // Loading the normalized chapter document directly is deterministic; the virtual
        // host remains available only as the base URL for CSS/images/fonts.
        StartupDiagnostics.Log($"Rendering EPUB spine {_spineIndex} directly: {_document.Spine[_spineIndex].RelativePath}; resourceMapping={_virtualHostReady}.");
        BookWebView.NavigateToString(rendered.Html);

        _book.SpineIndex = _spineIndex;
        _book.Progress = (_spineIndex + 1d) / _document.Spine.Count;
        App.Library.Save();
        ReaderProgress.Maximum = _document.Spine.Count;
        ReaderProgress.Value = _spineIndex + 1;
        ChapterProgressText.Text = string.Format(App.Localization.GetString("Reader_ChapterN"), _spineIndex + 1);
        ReaderPercentText.Text = $"{Math.Round(_book.Progress * 100)}%";
        BookProgressText.Text = string.Format(App.Localization.GetString("Reader_ReadPercent"), Math.Round(_book.Progress * 100));

        if (!string.IsNullOrWhiteSpace(fragmentValue))
            await ScrollToFragmentAfterNavigationAsync(fragmentValue);
    }

    private async Task ScrollToFragmentAfterNavigationAsync(string fragment)
    {
        // NavigateToString has no URI fragment. Wait for NavigationCompleted to build the DOM;
        // then move to the requested EPUB anchor without changing the top-level origin.
        if (BookWebView.CoreWebView2 is null) return;
        void Handler(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            sender.NavigationCompleted -= Handler;
            if (!args.IsSuccess || sender.CoreWebView2 is null) return;
            var id = JsonSerializer.Serialize(fragment);
            _ = sender.CoreWebView2.ExecuteScriptAsync($"document.getElementById({id})?.scrollIntoView({{block:'start'}});");
        }
        BookWebView.NavigationCompleted += Handler;
        await Task.CompletedTask;
    }

    private async void BookWebView_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (_document is null || !Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri)) return;
        if (!string.Equals(uri.Host, "pagearc.local", StringComparison.OrdinalIgnoreCase)) return;

        var path = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        var normalized = EpubPath.Normalize(path);
        var index = _document.Spine.ToList().FindIndex(item =>
            string.Equals(EpubPath.Normalize(item.RelativePath), normalized, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;

        args.Cancel = true;
        await NavigateToSpineAsync(index, uri.Fragment);
    }

    private async void BookWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (args.IsSuccess)
        {
            ReaderInfoBar.IsOpen = false;
            await ApplyReaderStyleAsync();
            StartupDiagnostics.Log($"EPUB spine {_spineIndex} rendered successfully.");
            return;
        }

        StartupDiagnostics.Log($"EPUB WebView direct chapter render failed: {args.WebErrorStatus}.");
        ReaderInfoBar.Severity = InfoBarSeverity.Error;
        ReaderInfoBar.Message = string.Format(App.Localization.GetString("Reader_ChapterLoadFailed"), args.WebErrorStatus);
        ReaderInfoBar.IsOpen = true;
    }

    private async Task ApplyReaderStyleAsync()
    {
        if (BookWebView.CoreWebView2 is null) return;
        var settings = App.Settings.Current;
        var colors = settings.ReadingTheme switch
        {
            "dark" => (Background: "#232A2A", Foreground: "#F2F6F6"),
            "sepia" => (Background: "#F4EAD3", Foreground: "#443A2E"),
            _ => (Background: "#F7FBFB", Foreground: "#172526")
        };
        var maxWidth = settings.PageWidth switch { "narrow" => "34rem", "wide" => "52rem", _ => "42rem" };
        var fontCss = settings.DefaultFont == "book" ? "inherit" : $"'{settings.DefaultFont.Replace("'", string.Empty)}'";
        var scale = settings.FontScale.ToString(CultureInfo.InvariantCulture) + "em";
        var lineHeight = settings.LineHeight.ToString(CultureInfo.InvariantCulture);

        var bg = JsonSerializer.Serialize(colors.Background);
        var fg = JsonSerializer.Serialize(colors.Foreground);
        var width = JsonSerializer.Serialize(maxWidth);
        var font = JsonSerializer.Serialize(fontCss);
        var size = JsonSerializer.Serialize(scale);
        var line = JsonSerializer.Serialize(lineHeight);

        var script = $$"""
        (() => {
          let s = document.getElementById('pagearc-reader-style');
          if (!s) {
            s = document.createElement('style');
            s.id = 'pagearc-reader-style';
            s.textContent = `html,body{min-height:100%;transition:background-color 160ms ease-out,color 160ms ease-out}html{background:var(--pa-bg)!important}body{background:var(--pa-bg)!important;color:var(--pa-fg)!important;max-width:var(--pa-width);margin:0 auto!important;padding:3.5rem 4.5rem 5rem!important;font-size:var(--pa-size)!important;line-height:var(--pa-line)!important;font-family:var(--pa-font)!important;box-sizing:border-box}img,svg{max-width:100%!important;height:auto!important}svg image{max-width:100%}`;
            document.head.appendChild(s);
          }
          const root = document.documentElement;
          root.style.setProperty('--pa-bg', {{bg}});
          root.style.setProperty('--pa-fg', {{fg}});
          root.style.setProperty('--pa-width', {{width}});
          root.style.setProperty('--pa-font', {{font}});
          root.style.setProperty('--pa-size', {{size}});
          root.style.setProperty('--pa-line', {{line}});
        })();
        """;
        await BookWebView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private static string NormalizeFragmentValue(string? fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment)) return string.Empty;
        var value = fragment.TrimStart('#');
        try { value = Uri.UnescapeDataString(value); } catch (UriFormatException) { }
        return value;
    }

    private void Back_Click(object sender, RoutedEventArgs e) => App.MainWindow?.ExitReader();
    private void Contents_Click(object sender, RoutedEventArgs e) => ContentsColumn.Width = ContentsColumn.Width.Value > 0 ? new GridLength(0) : new GridLength(260);
    private async void Previous_Click(object sender, RoutedEventArgs e) { if (_document is not null && _spineIndex > 0) await NavigateToSpineAsync(_spineIndex - 1); }
    private async void Next_Click(object sender, RoutedEventArgs e) { if (_document is not null && _spineIndex < _document.Spine.Count - 1) await NavigateToSpineAsync(_spineIndex + 1); }

    private async void TocList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_document is null || TocList.SelectedIndex < 0) return;
        if (_document.Toc.Count == 0) { await NavigateToSpineAsync(TocList.SelectedIndex); return; }
        var tocHref = _document.Toc[TocList.SelectedIndex].Href;
        var href = EpubPath.Normalize(tocHref);
        var index = _document.Spine.ToList().FindIndex(x => string.Equals(EpubPath.Normalize(x.RelativePath), href, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            var hash = tocHref.IndexOf('#');
            await NavigateToSpineAsync(index, hash >= 0 ? tocHref[hash..] : null);
        }
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
