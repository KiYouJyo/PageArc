using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using PageArc.Models;
using PageArc.Services;
using PageArc.Services.Conversion;

namespace PageArc.Pages;

public sealed partial class ReaderPage : Page
{
    private readonly FlowReaderEngine _readerEngine = new();
    private readonly ConversionRuntimeManager _conversionRuntimeManager = ConversionRuntimeManager.Shared;
    private readonly FlowSearchService _searchService = new();
    private readonly ObservableCollection<ReaderSearchListItem> _searchItems = [];
    private readonly ObservableCollection<ReaderBookmarkListItem> _bookmarkItems = [];
    private BookEntry? _book;
    private IFlowBookSource? _source;
    private FlowDocument? _document;
    private FlowSectionContent? _currentContent;
    private int _sectionIndex;
    private int _renderedSectionEndIndex;
    private double _sectionFraction;
    private bool _settingsReady;
    private bool _webReady;
    private bool _updatingTocSelection;
    private string? _mappedCacheRoot;
    private TaskCompletionSource<bool>? _navigationCompletion;
    private DateTimeOffset _lastProgressSave = DateTimeOffset.MinValue;
    private CancellationTokenSource? _searchCts;
    private ReaderSidebarMode _sidebarMode = ReaderSidebarMode.Contents;
    private bool _pageMeasurementInProgress;

    public ReaderPage()
    {
        StartupDiagnostics.Log("ReaderPage constructor entered.");
        try
        {
            InitializeComponent();
            SearchResultsList.ItemsSource = _searchItems;
            BookmarksList.ItemsSource = _bookmarkItems;
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

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
        SaveReadingPosition(force: true);
        var source = _source;
        _source = null;
        if (source is not null) _ = source.DisposeAsync().AsTask();
        base.OnNavigatedFrom(e);
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
        BookmarksHeading.Text = AutomationProperties.GetName(BookmarkButton);
        if (string.IsNullOrWhiteSpace(BookmarksHeading.Text)) BookmarksHeading.Text = "Bookmarks";
        App.Library.MarkOpened(_book);
        SelectByTag(ReaderThemeCombo, App.Settings.Current.ReadingTheme);
        ReaderFontScaleSlider.Value = App.Settings.Current.FontScale;
        ReaderLineHeightSlider.Value = App.Settings.Current.LineHeight;
        EnforceFixedReaderOptions();
        _settingsReady = true;

        if (!_readerEngine.CanOpen(_book))
        {
            ReaderInfoBar.Severity = InfoBarSeverity.Warning;
            ReaderInfoBar.Message = App.Localization.GetString("Reader_UnsupportedV01");
            ReaderInfoBar.IsOpen = true;
            return;
        }

        ReaderLoadingLayer.Visibility = Visibility.Visible;
        try
        {
            if (!await EnsureConversionRuntimeForBookAsync(_book))
            {
                ReaderLoadingLayer.Visibility = Visibility.Collapsed;
                return;
            }

            await EnsureWebViewAsync();
            _source = await _readerEngine.OpenAsync(_book);
            _document = _source.Document;
            if (_document.Sections.Count == 0)
                throw new InvalidDataException("This ebook does not contain any readable flow sections.");

            if (!string.IsNullOrWhiteSpace(_document.Title))
            {
                _book.Title = _document.Title;
                BookTitleText.Text = _document.Title;
            }
            if (!string.IsNullOrWhiteSpace(_document.Author)) _book.Author = _document.Author;
            App.Library.Save();

            TocList.ItemsSource = _document.Toc.Count > 0
                ? _document.Toc.Select(item => $"{new string('　', Math.Max(0, item.Depth))}{item.Title}").ToList()
                : _document.Sections.Select((_, i) => string.Format(App.Localization.GetString("Reader_ChapterN"), i + 1)).ToList();
            ContentsMetaText.Text = $"{_document.Sections.Count} · {_document.Format}";

            ConfigureBookResourceMapping();
            RefreshBookmarks();
            _pageMap = new FlowPageMap(_document);
            await MeasureAndFreezePageMapAsync();
            var requestedIndex = ResolveInitialSectionIndex(_document, _book);
            _sectionFraction = Math.Clamp(_book.SectionFraction, 0, 1);
            await NavigateToSectionAsync(requestedIndex, preferReadableText: true, restoreSavedFraction: true);
            StartupDiagnostics.Log($"Flow reader initialized: format={_document.Format}, sections={_document.Sections.Count}, toc={_document.Toc.Count}.");
        }
        catch (Exception ex)
        {
            ReaderLoadingLayer.Visibility = Visibility.Collapsed;
            StartupDiagnostics.Log("Flow reader initialization failed", ex);
            ShowReaderError(ex.Message);
        }
    }

    private async Task<bool> EnsureConversionRuntimeForBookAsync(BookEntry book)
    {
        var format = BookFormatRegistry.Normalize(book.Format);
        if (string.IsNullOrWhiteSpace(format)) format = BookFormatRegistry.FormatFromPath(book.FilePath);

        if (format is not ("MOBI" or "AZW3" or "LIT"))
            return true;
        if (new CalibreConversionProvider().IsAvailable || _conversionRuntimeManager.IsInstalled)
            return true;

        var sizeMb = ConversionRuntimeManager.ExpectedArchiveSize / (1024d * 1024d);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = ReaderLocalText("需要转换内核", "変換ランタイムが必要です", "Conversion runtime required"),
            Content = ReaderLocalText(
                $"首次打开 {format} 需要 PageArc Conversion Runtime。基础阅读器不包含该组件；将从独立仓库下载约 {sizeMb:0} MB，并在 SHA-256 校验后安装到本机用户目录。",
                $"{format} を初めて開くには PageArc Conversion Runtime が必要です。基本リーダーには含まれていません。独立リポジトリから約 {sizeMb:0} MB をダウンロードし、SHA-256 検証後にユーザー領域へインストールします。",
                $"Opening {format} for the first time requires PageArc Conversion Runtime. It is not included in the base reader; about {sizeMb:0} MB will be downloaded from the separate runtime repository and installed per-user after SHA-256 verification."),
            PrimaryButtonText = ReaderLocalText("下载并继续", "ダウンロードして続行", "Download and continue"),
            CloseButtonText = ReaderLocalText("取消", "キャンセル", "Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            ReaderInfoBar.Severity = InfoBarSeverity.Informational;
            ReaderInfoBar.Message = ReaderLocalText(
                "未安装转换内核，当前书本未打开。",
                "変換ランタイムをインストールしなかったため、この書籍は開いていません。",
                "The conversion runtime was not installed, so this book was not opened.");
            ReaderInfoBar.IsOpen = true;
            return false;
        }

        ReaderInfoBar.Severity = InfoBarSeverity.Informational;
        ReaderInfoBar.IsOpen = true;
        try
        {
            var progress = new Progress<ConversionRuntimeProgress>(value =>
            {
                var percent = value.TotalBytes is > 0 ? value.Fraction * 100 : 0;
                ReaderInfoBar.Message = value.Stage switch
                {
                    "manifest" => ReaderLocalText("正在检查转换内核…", "変換ランタイムを確認しています…", "Checking conversion runtime…"),
                    "download" => ReaderLocalText(
                        $"正在下载转换内核… {percent:0}%",
                        $"変換ランタイムをダウンロードしています… {percent:0}%",
                        $"Downloading conversion runtime… {percent:0}%"),
                    "extract" => ReaderLocalText("正在校验并安装转换内核…", "変換ランタイムを検証してインストールしています…", "Verifying and installing conversion runtime…"),
                    "complete" => ReaderLocalText("转换内核安装完成，正在打开书本…", "変換ランタイムのインストール完了。書籍を開いています…", "Conversion runtime installed; opening the book…"),
                    _ => ReaderLocalText("正在准备转换内核…", "変換ランタイムを準備しています…", "Preparing conversion runtime…")
                };
            });

            await _conversionRuntimeManager.EnsureInstalledAsync(progress);
            return true;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Reader on-demand conversion runtime installation failed", ex);
            ReaderInfoBar.Severity = InfoBarSeverity.Error;
            ReaderInfoBar.Message = ReaderLocalText(
                "转换内核下载或校验失败，无法打开此书。",
                "変換ランタイムのダウンロードまたは検証に失敗したため、この書籍を開けません。",
                "The conversion runtime could not be downloaded or verified, so this book cannot be opened.");
            ReaderInfoBar.IsOpen = true;
            return false;
        }
    }

    private static string ReaderLocalText(string zh, string ja, string en)
    {
        var language = CultureInfo.CurrentUICulture.Name;
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return zh;
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return ja;
        return en;
    }

    private static void SelectByTag(ComboBox comboBox, string tag)
    {
        comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(x.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            ?? comboBox.Items.FirstOrDefault();
    }

    private async Task EnsureWebViewAsync()
    {
        if (_webReady) return;
        await ReaderWebView.EnsureCoreWebView2Async();
        var core = ReaderWebView.CoreWebView2 ?? throw new InvalidOperationException("WebView2 could not be initialized.");
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsWebMessageEnabled = true;

        ReaderWebView.NavigationStarting += (_, args) =>
        {
            if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri)) return;
            if (uri.Scheme is "http" or "https" && !string.Equals(uri.Host, "pagearc.local", StringComparison.OrdinalIgnoreCase))
                args.Cancel = true;
        };
        ReaderWebView.NavigationCompleted += (_, args) =>
        {
            var completion = _navigationCompletion;
            _navigationCompletion = null;
            if (completion is null) return;
            if (args.IsSuccess) completion.TrySetResult(true);
            else completion.TrySetException(new InvalidOperationException($"Reader navigation failed: {args.WebErrorStatus}."));
        };
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.WebMessageReceived += (_, args) => HandleWebMessage(args.TryGetWebMessageAsString());
        core.AddWebResourceRequestedFilter("*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, args) =>
        {
            if (!Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var uri)) return;
            if (uri.Scheme is not ("http" or "https") || string.Equals(uri.Host, "pagearc.local", StringComparison.OrdinalIgnoreCase)) return;
            args.Response = core.Environment.CreateWebResourceResponse(Stream.Null, 403, "Blocked", "Content-Type: text/plain");
        };
        _webReady = true;
    }

    private void ConfigureBookResourceMapping()
    {
        if (!_webReady || _document?.CacheRoot is not { Length: > 0 } root) return;
        var fullRoot = Path.GetFullPath(root);
        if (string.Equals(_mappedCacheRoot, fullRoot, StringComparison.OrdinalIgnoreCase)) return;
        ReaderWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "pagearc.local",
            fullRoot,
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
        _mappedCacheRoot = fullRoot;
    }

    private async Task MeasureAndFreezePageMapAsync()
    {
        if (_document is null || _source is null || ReaderWebView.CoreWebView2 is null) return;
        var map = _pageMap ??= new FlowPageMap(_document);
        var measuredPages = new int[_document.Sections.Count];
        _pageMeasurementInProgress = true;
        try
        {
            for (var index = 0; index < _document.Sections.Count; index++)
            {
                var content = await _source.LoadSectionAsync(index);
                _navigationCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                ReaderWebView.CoreWebView2.NavigateToString(content.Html);
                await _navigationCompletion.Task;
                await ApplyWebReaderStyleAsync(0, forcePaginated: true);
                await ApplyReaderViewEnhancementsAsync("single");

                var result = await ReaderWebView.CoreWebView2.ExecuteScriptAsync("""
                    (async () => {
                      try { await document.fonts?.ready; } catch {}
                      await Promise.all(Array.from(document.images || []).map(image => {
                        if (image.complete) return image.decode?.().catch(() => {}) ?? Promise.resolve();
                        return new Promise(resolve => {
                          image.addEventListener('load', resolve, {once:true});
                          image.addEventListener('error', resolve, {once:true});
                        });
                      }));
                      await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
                      const metrics = window.__pagearc?.metrics?.();
                      return Math.max(1, Number(metrics?.pages) || 1);
                    })()
                    """);
                measuredPages[index] = int.TryParse(result, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pages)
                    ? Math.Max(1, pages)
                    : 1;
            }

            map.FreezeMeasuredPages(measuredPages);
            StartupDiagnostics.Log($"Flow pagination frozen: sections={measuredPages.Length}, pages={map.TotalPages}.");
            UpdateProgressUi(save: false);
        }
        finally
        {
            _pageMeasurementInProgress = false;
        }
    }

    private async Task NavigateToSectionAsync(int index, bool preferReadableText = false, bool restoreSavedFraction = false)
    {
        if (_document is null || _source is null || _book is null) return;

        ReaderLoadingLayer.Visibility = Visibility.Visible;
        ReaderInfoBar.IsOpen = false;
        try
        {
            var targetIndex = Math.Clamp(index, 0, _document.Sections.Count - 1);
            var content = await _source.LoadSectionAsync(targetIndex);
            if (preferReadableText
                && string.IsNullOrWhiteSpace(content.PlainText)
                && !ContainsVisualContent(content.Html))
            {
                var readable = await FindReadableSectionAsync(targetIndex);
                if (readable is not null)
                {
                    targetIndex = readable.Value.Index;
                    content = readable.Value.Content;
                }
            }

            if (IsReaderSpreadLayout)
            {
                var spreadStart = ReaderLayoutPolicy.ResolveSpreadStartIndex(
                    targetIndex,
                    _document.Sections.Count,
                    App.Settings.Current.ReaderSpreadMode);
                if (spreadStart != targetIndex)
                {
                    targetIndex = spreadStart;
                    content = await _source.LoadSectionAsync(targetIndex);
                }
            }

            var renderedContent = content;
            var renderedEndIndex = targetIndex;
            if (IsReaderSpreadLayout)
            {
                if (ReaderLayoutPolicy.HasLeadingBlankPage(targetIndex, App.Settings.Current.ReaderSpreadMode))
                {
                    renderedContent = CreateLeadingBlankSpread(content);
                }
                else if (targetIndex + 1 < _document.Sections.Count)
                {
                    var partner = await _source.LoadSectionAsync(targetIndex + 1);
                    renderedEndIndex = targetIndex + 1;
                    renderedContent = CombineSpreadSections(content, partner);
                }
            }

            _sectionIndex = targetIndex;
            _renderedSectionEndIndex = renderedEndIndex;
            _measuredAbsolutePage = null;
            _currentContent = renderedContent;
            if (!restoreSavedFraction) _sectionFraction = 0;
            _book.SpineIndex = _sectionIndex;
            _book.SectionFraction = _sectionFraction;

            _navigationCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ReaderWebView.CoreWebView2.NavigateToString(renderedContent.Html);
            await _navigationCompletion.Task;
            await ApplyWebReaderStyleAsync(_sectionFraction);
            // NavigationCompleted can run before the base reader state exists.
            // Re-apply page geometry after it so column width and turn distance
            // are always derived from the final WebView viewport.
            await ApplyReaderViewEnhancementsAsync();
            UpdateTocSelection();
            UpdateProgressUi(save: true);
            StartupDiagnostics.Log($"Flow render succeeded for section {_sectionIndex}-{_renderedSectionEndIndex}: {_document.Sections[_sectionIndex].Href}, chars={renderedContent.PlainText.Length}.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"Flow render failed for section {index}", ex);
            ShowReaderError(ex.Message);
        }
        finally
        {
            ReaderLoadingLayer.Visibility = Visibility.Collapsed;
        }
    }

    private async Task<(int Index, FlowSectionContent Content)?> FindReadableSectionAsync(int requestedIndex)
    {
        if (_document is null || _source is null) return null;
        for (var i = requestedIndex + 1; i < _document.Sections.Count; i++)
        {
            var candidate = await _source.LoadSectionAsync(i);
            if (!string.IsNullOrWhiteSpace(candidate.PlainText)) return (i, candidate);
        }
        for (var i = requestedIndex - 1; i >= 0; i--)
        {
            var candidate = await _source.LoadSectionAsync(i);
            if (!string.IsNullOrWhiteSpace(candidate.PlainText)) return (i, candidate);
        }
        return null;
    }

    private static FlowSectionContent CombineSpreadSections(FlowSectionContent first, FlowSectionContent second)
    {
        var firstBody = ExtractBodyFragment(first.Html);
        var secondBody = ResolveFragmentResources(
            ExtractBodyFragment(second.Html),
            ExtractBaseHref(second.Html));
        var firstHead = ExtractHeadFragment(first.Html);
        var html = $"<!doctype html><html><head>{firstHead}</head><body>" +
                   $"<section class=\"pagearc-spread-page pagearc-spread-left\">{firstBody}</section>" +
                   $"<section class=\"pagearc-spread-page pagearc-spread-right\">{secondBody}</section>" +
                   "</body></html>";
        return new FlowSectionContent(
            html,
            string.Join(Environment.NewLine + Environment.NewLine, new[] { first.PlainText, second.PlainText }.Where(text => !string.IsNullOrWhiteSpace(text))),
            first.BaseHref);
    }

    private static FlowSectionContent CreateLeadingBlankSpread(FlowSectionContent firstPage)
    {
        var body = ExtractBodyFragment(firstPage.Html);
        var head = ExtractHeadFragment(firstPage.Html);
        var html = $"<!doctype html><html><head>{head}</head><body>" +
                   "<section class=\"pagearc-spread-page pagearc-spread-left pagearc-spread-blank\" aria-hidden=\"true\"></section>" +
                   $"<section class=\"pagearc-spread-page pagearc-spread-right\">{body}</section>" +
                   "</body></html>";
        return new FlowSectionContent(html, firstPage.PlainText, firstPage.BaseHref);
    }

    private static bool ContainsVisualContent(string html) =>
        Regex.IsMatch(html ?? string.Empty, @"<(?:img|svg|image|video)\b", RegexOptions.IgnoreCase);

    private static string ExtractBodyFragment(string html)
    {
        var match = Regex.Match(html ?? string.Empty, @"<body\b[^>]*>(?<content>.*?)</body\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["content"].Value : html ?? string.Empty;
    }

    private static string ExtractHeadFragment(string html)
    {
        var match = Regex.Match(html ?? string.Empty, @"<head\b[^>]*>(?<content>.*?)</head\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["content"].Value : "<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">";
    }

    private static string ExtractBaseHref(string html)
    {
        var match = Regex.Match(html ?? string.Empty, @"<base\b[^>]*\bhref\s*=\s*[\""'](?<href>[^\""']+)[\""']", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["href"].Value : string.Empty;
    }

    private static string ResolveFragmentResources(string fragment, string baseHref)
    {
        if (string.IsNullOrWhiteSpace(baseHref) || !Uri.TryCreate(baseHref, UriKind.Absolute, out var baseUri)) return fragment;
        return Regex.Replace(
            fragment,
            @"(?<prefix>\bsrc\s*=\s*[\""'])(?<url>[^\""']+)(?<suffix>[\""'])",
            match =>
            {
                var value = match.Groups["url"].Value;
                if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("https:", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("//", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("#", StringComparison.Ordinal)) return match.Value;
                return match.Groups["prefix"].Value + new Uri(baseUri, value).AbsoluteUri + match.Groups["suffix"].Value;
            },
            RegexOptions.IgnoreCase);
    }

    private bool IsReaderSpreadLayout =>
        App.Settings.Current.ReaderViewMode == "horizontal"
        && App.Settings.Current.ReaderSpreadMode is "odd" or "even";

    private int NextReaderSectionIndex() => IsReaderSpreadLayout
        ? Math.Max(_sectionIndex + 1, _renderedSectionEndIndex + 1)
        : _sectionIndex + 1;

    private int PreviousReaderSectionIndex() => IsReaderSpreadLayout
        ? ReaderLayoutPolicy.ResolvePreviousSpreadStartIndex(_sectionIndex, App.Settings.Current.ReaderSpreadMode)
        : _sectionIndex - 1;

    private static int ResolveInitialSectionIndex(FlowDocument document, BookEntry book)
    {
        var saved = Math.Clamp(book.SpineIndex, 0, Math.Max(0, document.Sections.Count - 1));
        if (saved > 0 || book.Progress > 0.001 || book.SectionFraction > 0.001) return saved;
        return document.Toc.Select(item => item.SectionIndex).FirstOrDefault(index => index is >= 0) ?? saved;
    }

    private async Task ApplyWebReaderStyleAsync(double restoreFraction, bool forcePaginated = false)
    {
        if (!_webReady) return;
        var settings = App.Settings.Current;
        var (background, foreground) = settings.ReadingTheme switch
        {
            "dark" => ("#232A2A", "#F2F6F6"),
            "sepia" => ("#F4EAD3", "#443A2E"),
            _ => ("#FFFFFF", "#202020")
        };
        var fontFamily = settings.DefaultFont switch
        {
            "Segoe UI Variable" => "'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif",
            "Georgia" => "Georgia, serif",
            _ => "inherit"
        };
        var scale = Math.Clamp(settings.FontScale, 0.8, 1.6).ToString("0.###", CultureInfo.InvariantCulture);
        var lineHeight = Math.Clamp(settings.LineHeight, 1.2, 2.4).ToString("0.###", CultureInfo.InvariantCulture);
        // Continuous scrolling stays enabled in persisted settings. A spread
        // selection is the one deliberate exception: it switches the reader
        // surface to paginated columns so the option has a visible effect.
        var continuous = !forcePaginated && settings.ReaderViewMode != "horizontal";
        var fontRule = fontFamily == "inherit" ? string.Empty : $"font-family:{fontFamily}!important;";
        var modeCss = continuous
            ? "html,body{min-height:100%;overflow-x:hidden;}body{height:auto;}"
            : "html{width:100%;height:100%;overflow:hidden;scroll-behavior:auto;}body{width:100%;height:100%;min-width:0;max-width:none;column-count:1;column-width:auto;column-gap:0;column-fill:auto;overflow:visible;}html::-webkit-scrollbar{display:none;}";

        var css = """
            :root{color-scheme:__SCHEME__;}
            html,body{margin:0;background:__BACKGROUND__!important;color:__FOREGROUND__!important;}
            html{font-size:__SCALE__em;}
            body{box-sizing:border-box;padding:44px 56px 56px;line-height:__LINE_HEIGHT__;__FONT_RULE__overflow-wrap:anywhere;}
            body :where(p,div,li,dd,dt,blockquote){line-height:__LINE_HEIGHT__!important;}
            body *{max-width:100%;}
            img,svg,video{height:auto!important;max-width:100%!important;}
            table{max-width:100%;border-collapse:collapse;}
            pre{white-space:pre-wrap;overflow-wrap:anywhere;}
            blockquote{margin-inline:28px;}
            a{color:#005FB8;}
            mark.pagearc-search-match{background:rgba(255,209,69,.34);color:inherit;border-radius:2px;}
            __MODE_CSS__
            """
            .Replace("__SCHEME__", settings.ReadingTheme == "dark" ? "dark" : "light", StringComparison.Ordinal)
            .Replace("__BACKGROUND__", background, StringComparison.Ordinal)
            .Replace("__FOREGROUND__", foreground, StringComparison.Ordinal)
            .Replace("__SCALE__", scale, StringComparison.Ordinal)
            .Replace("__LINE_HEIGHT__", lineHeight, StringComparison.Ordinal)
            .Replace("__FONT_RULE__", fontRule, StringComparison.Ordinal)
            .Replace("__MODE_CSS__", modeCss, StringComparison.Ordinal);

        var cssJson = JsonSerializer.Serialize(css);
        var fraction = Math.Clamp(restoreFraction, 0, 1).ToString("0.######", CultureInfo.InvariantCulture);
        var script = """
            (() => {
              let style = document.getElementById('pagearc-reader-style');
              if (!style) { style = document.createElement('style'); style.id = 'pagearc-reader-style'; document.head.appendChild(style); }
              style.textContent = __CSS_JSON__;
              const continuous = __CONTINUOUS__;
              const root = document.scrollingElement || document.documentElement;
              const clamp = v => Math.max(0, Math.min(1, Number.isFinite(v) ? v : 0));
              const state = window.__pagearc || {};
              const progress = () => {
                const max = continuous ? Math.max(0, root.scrollHeight - root.clientHeight) : Math.max(0, root.scrollWidth - root.clientWidth);
                const pos = continuous ? root.scrollTop : root.scrollLeft;
                return max <= 1 ? 0 : clamp(pos / max);
              };
              const metrics = () => {
                const visible = Math.max(1, state.visiblePageCount || 1);
                if (state.continuous) {
                  const pages = Math.max(1, Math.ceil(root.scrollHeight / Math.max(1, root.clientHeight)));
                  const page = Math.max(0, Math.min(pages - 1, Math.round(root.scrollTop / Math.max(1, root.clientHeight))));
                  return { page, pages, visible: 1 };
                }
                const step = Math.max(1, state.pageStep || root.clientWidth);
                const groups = Math.max(1, Math.floor((Math.max(0, root.scrollWidth - root.clientWidth) + 2) / step) + 1);
                const group = Math.max(0, Math.min(groups - 1, Math.round(root.scrollLeft / step)));
                return { page: group * visible, pages: groups * visible, visible };
              };
              const notify = () => {
                const value = metrics();
                window.chrome?.webview?.postMessage('pagearc-progress:' + JSON.stringify({
                  fraction: progress(), page: value.page, pages: value.pages, visible: value.visible
                }));
              };
              state.root = root;
              state.continuous = continuous;
              state.progress = progress;
              state.metrics = metrics;
              state.notify = notify;
              state.move = delta => {
                const max = continuous ? Math.max(0, root.scrollHeight - root.clientHeight) : Math.max(0, root.scrollWidth - root.clientWidth);
                const pos = continuous ? root.scrollTop : root.scrollLeft;
                if ((delta < 0 && pos <= 2) || (delta > 0 && pos >= max - 2) || max <= 1) return false;
                const amount = (continuous ? root.clientHeight * 0.85 : (state.pageStep || root.clientWidth)) * delta;
                if (continuous) root.scrollTo({top: Math.max(0, Math.min(max, pos + amount)), behavior:'smooth'});
                else root.scrollTo({left: Math.max(0, Math.min(max, pos + amount)), behavior:'auto'});
                setTimeout(() => state.notify(), 180);
                return true;
              };
              state.snap = () => {
                if (continuous) return;
                const max = Math.max(0, root.scrollWidth - root.clientWidth);
                const step = Math.max(1, state.pageStep || root.clientWidth);
                const lastCompleteStep = Math.max(0, Math.floor((max + 2) / step));
                const page = Math.max(0, Math.min(lastCompleteStep, Math.round(root.scrollLeft / step)));
                const target = page * step;
                if (Math.abs(root.scrollLeft - target) > 1) root.scrollTo(target, 0);
              };
              state.restorePage = page => {
                if (continuous) return state.restore(progress());
                const max = Math.max(0, root.scrollWidth - root.clientWidth);
                const step = Math.max(1, state.pageStep || root.clientWidth);
                root.scrollTo(Math.max(0, Math.min(max, Math.max(0, Math.round(page)) * step)), 0);
                state.snap();
                state.notify();
              };
              state.restore = value => {
                const max = continuous ? Math.max(0, root.scrollHeight - root.clientHeight) : Math.max(0, root.scrollWidth - root.clientWidth);
                const target = max * clamp(value);
                if (continuous) {
                  root.scrollTo(0, target);
                } else {
                  // Reflowing text changes column widths. A raw fractional
                  // restore can then stop between columns and reveal the
                  // previous page. Always restore to a complete page/spread.
                  const step = Math.max(1, state.pageStep || root.clientWidth);
                  const lastCompleteStep = Math.max(0, Math.floor((max + 2) / step));
                  const page = Math.max(0, Math.min(lastCompleteStep, Math.round(target / step)));
                  root.scrollTo(page * step, 0);
                }
                state.notify();
              };
              if (!state.listenersInstalled) {
                let queued = false;
                state.onScroll = () => { if (queued) return; queued = true; requestAnimationFrame(() => { queued = false; state.snap(); state.notify(); }); };
              state.onResize = () => {
                if (state.continuous) {
                  state.restore(state.progress());
                  return;
                }
                const step = Math.max(1, state.pageStep || root.clientWidth);
                state.restorePage(Math.round(root.scrollLeft / step));
              };
                root.addEventListener('scroll', state.onScroll, {passive:true});
                window.addEventListener('resize', state.onResize);
                state.listenersInstalled = true;
              }
              window.__pagearc = state;
              state.restore(__FRACTION__);
              return true;
            })()
            """
            .Replace("__CSS_JSON__", cssJson, StringComparison.Ordinal)
            .Replace("__CONTINUOUS__", continuous ? "true" : "false", StringComparison.Ordinal)
            .Replace("__FRACTION__", fraction, StringComparison.Ordinal);
        await ReaderWebView.CoreWebView2.ExecuteScriptAsync(script);
        ApplyFigmaReaderPageGeometry();
    }

    private void HandleWebMessage(string? message)
    {
        if (_document is null || _book is null || string.IsNullOrWhiteSpace(message)) return;
        double fraction;
        if (message.StartsWith("pagearc-progress:", StringComparison.Ordinal))
        {
            if (_pageMeasurementInProgress) return;
            try
            {
                using var payload = JsonDocument.Parse(message["pagearc-progress:".Length..]);
                fraction = payload.RootElement.GetProperty("fraction").GetDouble();
                var page = Math.Max(0, payload.RootElement.GetProperty("page").GetInt32());
                var pages = Math.Max(1, payload.RootElement.GetProperty("pages").GetInt32());
                var map = EnsurePageMap();
                if (map is not null)
                {
                    map.UpdateRenderedRange(_sectionIndex, Math.Max(_sectionIndex, _renderedSectionEndIndex), pages);
                    _measuredAbsolutePage = map.GetSectionStartPage(_sectionIndex) + Math.Min(page, pages - 1);
                }
            }
            catch (JsonException)
            {
                return;
            }
        }
        else
        {
            if (!message.StartsWith("progress:", StringComparison.Ordinal)
                || !double.TryParse(message.AsSpan("progress:".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out fraction)) return;
        }
        _sectionFraction = Math.Clamp(fraction, 0, 1);
        _book.SectionFraction = _sectionFraction;
        UpdateProgressUi(save: false);
        SaveReadingPosition(force: false);
    }

    private async Task<bool> TryMoveWithinSectionAsync(int delta)
    {
        if (!_webReady) return false;
        try
        {
            var result = await ReaderWebView.CoreWebView2.ExecuteScriptAsync($"window.__pagearc ? window.__pagearc.move({delta}) : false");
            return string.Equals(result.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void UpdateTocSelection()
    {
        if (_document is null) return;
        _updatingTocSelection = true;
        try
        {
            if (_document.Toc.Count == 0)
            {
                TocList.SelectedIndex = _sectionIndex;
                return;
            }
            var index = _document.Toc.ToList().FindLastIndex(item => item.SectionIndex is int section && section <= _sectionIndex);
            TocList.SelectedIndex = index;
        }
        finally
        {
            _updatingTocSelection = false;
        }
    }

    private void UpdateProgressUi(bool save)
    {
        if (_document is null || _book is null) return;
        _book.SpineIndex = _sectionIndex;
        _book.SectionFraction = _sectionFraction;
        var map = EnsurePageMap();
        var page = _measuredAbsolutePage ?? map?.GetPage(_sectionIndex, _sectionFraction) ?? 1;
        var totalPages = Math.Max(1, map?.TotalPages ?? _document.Sections.Count);
        _book.Progress = totalPages <= 1 ? 0 : Math.Clamp((page - 1) / (double)(totalPages - 1), 0, 1);
        ReaderProgress.Value = _book.Progress;
        ChapterProgressText.Text = FlowSearchService.ResolveChapterTitle(_document, _sectionIndex);
        var percent = Math.Round(_book.Progress * 100);
        ReaderPercentText.Text = $"{percent}%";
        BookProgressText.Text = string.Format(App.Localization.GetString("Reader_ReadPercent"), percent);
        UpdatePageJumpUi();
        if (save) SaveReadingPosition(force: true);
    }

    private void SaveReadingPosition(bool force)
    {
        if (_book is null) return;
        var now = DateTimeOffset.UtcNow;
        if (!force && now - _lastProgressSave < TimeSpan.FromSeconds(1)) return;
        _lastProgressSave = now;
        App.Library.Save();
    }

    private void ShowSidebar(ReaderSidebarMode mode)
    {
        _sidebarMode = mode;
        ContentsColumn.Width = new GridLength(260);
        ContentsMode.Visibility = mode == ReaderSidebarMode.Contents ? Visibility.Visible : Visibility.Collapsed;
        SearchMode.Visibility = mode == ReaderSidebarMode.Search ? Visibility.Visible : Visibility.Collapsed;
        BookmarksMode.Visibility = mode == ReaderSidebarMode.Bookmarks ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshBookmarks()
    {
        _bookmarkItems.Clear();
        if (_book is null || _document is null)
        {
            BookmarksMetaText.Text = "0";
            BookmarksFooterText.Text = "0";
            return;
        }

        var count = Math.Max(1, _document.Sections.Count);
        foreach (var bookmark in App.ReadingData.GetBookmarks(_book.Id))
        {
            var progress = Math.Clamp((bookmark.Locator.SectionIndex + bookmark.Locator.Fraction) / count, 0, 1);
            _bookmarkItems.Add(new ReaderBookmarkListItem(bookmark, progress));
        }
        BookmarksMetaText.Text = _bookmarkItems.Count.ToString(CultureInfo.CurrentCulture);
        BookmarksFooterText.Text = _bookmarkItems.Count.ToString(CultureInfo.CurrentCulture);
    }

    private string BuildCurrentSnippet()
    {
        var text = _currentContent?.PlainText ?? string.Empty;
        if (text.Length == 0) return string.Empty;
        var index = Math.Clamp((int)Math.Round(_sectionFraction * Math.Max(0, text.Length - 1)), 0, Math.Max(0, text.Length - 1));
        return FlowSearchService.BuildSnippet(text, index, 0, 34);
    }

    private async Task HighlightSearchResultAsync(FlowSearchResult result)
    {
        if (!_webReady) return;
        var script = """
            (() => {
              document.querySelectorAll('mark.pagearc-search-match').forEach(mark => mark.replaceWith(document.createTextNode(mark.textContent || '')));
              const needle = __MATCH_JSON__.toLocaleLowerCase();
              if (!needle) return false;
              const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
              let node;
              let seen = 0;
              while ((node = walker.nextNode())) {
                const source = node.nodeValue || '';
                const lower = source.toLocaleLowerCase();
                let from = 0;
                while (from <= lower.length - needle.length) {
                  const index = lower.indexOf(needle, from);
                  if (index < 0) break;
                  if (seen++ === __OCCURRENCE__) {
                    const range = document.createRange();
                    range.setStart(node, index);
                    range.setEnd(node, index + needle.length);
                    const mark = document.createElement('mark');
                    mark.className = 'pagearc-search-match';
                    range.surroundContents(mark);
                    mark.scrollIntoView({block:'center', inline:'center'});
                    setTimeout(() => window.chrome?.webview?.postMessage('progress:' + (window.__pagearc?.progress?.() ?? 0).toFixed(6)), 60);
                    return true;
                  }
                  from = index + Math.max(1, needle.length);
                }
              }
              return false;
            })()
            """
            .Replace("__MATCH_JSON__", JsonSerializer.Serialize(result.MatchText), StringComparison.Ordinal)
            .Replace("__OCCURRENCE__", Math.Max(0, result.OccurrenceInSection).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        await ReaderWebView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private void ShowReaderError(string message)
    {
        ReaderInfoBar.Severity = InfoBarSeverity.Error;
        ReaderInfoBar.Message = message;
        ReaderInfoBar.IsOpen = true;
    }

    private void Back_Click(object sender, RoutedEventArgs e) => App.MainWindow?.ExitReader();

    private void Contents_Click(object sender, RoutedEventArgs e)
    {
        if (_sidebarMode == ReaderSidebarMode.Contents && ContentsColumn.Width.Value > 0)
            ContentsColumn.Width = new GridLength(0);
        else
            ShowSidebar(ReaderSidebarMode.Contents);
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        ShowSidebar(ReaderSidebarMode.Search);
        ReaderSearchBox.Focus(FocusState.Programmatic);
    }

    private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_source is null) return;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        var query = sender.Text.Trim();

        if (query.Length == 0)
        {
            _searchItems.Clear();
            SearchCountText.Text = "0";
            SearchFooterText.Text = "0";
            return;
        }

        try
        {
            await Task.Delay(220, token);
            var results = await _searchService.SearchAsync(_source, query, 200, token);
            token.ThrowIfCancellationRequested();
            _searchItems.Clear();
            for (var i = 0; i < results.Count; i++)
                _searchItems.Add(new ReaderSearchListItem(results[i], i + 1, results.Count));
            SearchCountText.Text = results.Count.ToString(CultureInfo.CurrentCulture);
            SearchFooterText.Text = results.Count.ToString(CultureInfo.CurrentCulture);
        }
        catch (OperationCanceledException)
        {
            // A newer query replaced this one.
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Reader search failed", ex);
            SearchCountText.Text = "—";
        }
    }

    private async void SearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResultsList.SelectedItem is not ReaderSearchListItem item) return;
        SearchResultsList.SelectedItem = null;
        _sectionFraction = item.Result.Fraction;
        await NavigateToSectionAsync(item.Result.SectionIndex, restoreSavedFraction: true);
        await HighlightSearchResultAsync(item.Result);
    }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (_book is null || _document is null) return;
        var snippet = BuildCurrentSnippet();
        var locator = new FlowContentLocator(_sectionIndex, _sectionFraction, TextQuote: snippet);
        var chapterTitle = FlowSearchService.ResolveChapterTitle(_document, _sectionIndex);
        var bookmark = App.ReadingData.ToggleBookmark(_book.Id, locator, chapterTitle, snippet);
        RefreshBookmarks();
        ShowSidebar(ReaderSidebarMode.Bookmarks);
        if (bookmark is not null)
        {
            ReaderInfoBar.Severity = InfoBarSeverity.Success;
            ReaderInfoBar.Message = App.Localization.GetString("Reader_BookmarkSaved");
            ReaderInfoBar.IsOpen = true;
        }
    }

    private async void BookmarksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BookmarksList.SelectedItem is not ReaderBookmarkListItem item) return;
        BookmarksList.SelectedItem = null;
        _sectionFraction = item.Bookmark.Locator.Fraction;
        await NavigateToSectionAsync(item.Bookmark.Locator.SectionIndex, restoreSavedFraction: true);
    }

    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (await TryMoveWithinSectionAsync(-1)) return;
        if (_document is not null && PreviousReaderSectionIndex() < _sectionIndex)
            await NavigateToSectionAsync(PreviousReaderSectionIndex(), restoreSavedFraction: false);
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (await TryMoveWithinSectionAsync(1)) return;
        var nextIndex = NextReaderSectionIndex();
        if (_document is not null && nextIndex < _document.Sections.Count)
            await NavigateToSectionAsync(nextIndex, restoreSavedFraction: false);
    }

    private async void TocList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingTocSelection || _document is null || TocList.SelectedIndex < 0) return;
        if (_document.Toc.Count == 0)
        {
            await NavigateToSectionAsync(TocList.SelectedIndex, preferReadableText: true);
            return;
        }
        if (_document.Toc[TocList.SelectedIndex].SectionIndex is int sectionIndex)
            await NavigateToSectionAsync(sectionIndex, preferReadableText: true);
    }

    private async void ReaderThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => await SaveAndApplyReaderSettingsAsync();
    private async void ReaderFontScaleSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => await SaveAndApplyReaderSettingsAsync();
    private async void ReaderLineHeightSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => await SaveAndApplyReaderSettingsAsync();
    private async Task SaveAndApplyReaderSettingsAsync()
    {
        if (!_settingsReady) return;
        App.Settings.Update(settings =>
        {
            if (ReaderThemeCombo.SelectedItem is ComboBoxItem { Tag: string theme }) settings.ReadingTheme = theme;
            settings.FontScale = ReaderFontScaleSlider.Value;
            settings.LineHeight = ReaderLineHeightSlider.Value;
            settings.ContinuousScrolling = true;
            settings.ShowReadingProgress = true;
        });
        if (_webReady) await ApplyWebReaderStyleAsync(_sectionFraction);
    }

    private enum ReaderSidebarMode
    {
        Contents,
        Search,
        Bookmarks
    }
}
