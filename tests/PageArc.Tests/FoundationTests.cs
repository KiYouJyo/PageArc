using System.IO.Compression;
using System.Xml.Linq;
using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class FoundationTests
{
    [Theory]
    [InlineData("v0.1.0", 0, 1, 0)]
    [InlineData("0.2.3-preview.1", 0, 2, 3)]
    [InlineData("V1.4.2+build", 1, 4, 2)]
    public void VersionParser_ParsesReleaseTags(string tag, int major, int minor, int build)
    {
        Assert.True(VersionParser.TryParseTag(tag, out var version));
        Assert.Equal(new Version(major, minor, build, 0), version);
    }

    [Theory]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("ja-JP", "ja-JP")]
    [InlineData("en-US", "en-US")]
    [InlineData("fr-FR", "system")]
    [InlineData(null, "system")]
    public void LanguagePreference_Normalizes(string? value, string expected)
    {
        Assert.Equal(expected, LanguagePreference.Normalize(value));
    }

    [Theory]
    [InlineData("OEBPS", "Text/Chapter%201.xhtml#start", "OEBPS/Text/Chapter 1.xhtml")]
    [InlineData("OPS/nav", "../Text/%E6%97%A5%E6%9C%AC.xhtml", "OPS/Text/日本.xhtml")]
    [InlineData("", "EPUB\\chapter.xhtml", "EPUB/chapter.xhtml")]
    public void EpubPath_CombinesAndDecodesPackageReferences(string directory, string href, string expected)
    {
        Assert.Equal(expected, EpubPath.Combine(directory, href));
    }

    [Fact]
    public void EpubPath_EncodesWebNavigationOnce()
    {
        Assert.Equal("OPS/Text/Chapter%201.xhtml", EpubPath.ToWebPath("OPS/Text/Chapter 1.xhtml"));
        Assert.Equal("OPS/Text/%E6%97%A5%E6%9C%AC.xhtml", EpubPath.ToWebPath("OPS/Text/日本.xhtml"));
    }

    [Fact]
    public void EpubWebRenderer_NormalizesXhtmlForWebView()
    {
        const string source = "<?xml version=\"1.0\" encoding=\"utf-8\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>X</title></head><body><img src=\"images/a.png\"/><p>Hello</p></body></html>";
        var html = EpubWebRenderer.NormalizeForWebView(source, "https://pagearc.local/OEBPS/Text/Chapter.xhtml");

        Assert.DoesNotContain("<?xml", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<meta charset=\"utf-8\">", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<base href=\"https://pagearc.local/OEBPS/Text/Chapter.xhtml\">", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("images/a.png", html, StringComparison.Ordinal);
        Assert.Equal("Hello", EpubWebRenderer.ExtractReadableText(source));
    }

    [Fact]
    public async Task EpubPipeline_ParsesEncodedSpineAndBuildsRenderableChapter()
    {
        var epubPath = Path.Combine(Path.GetTempPath(), $"pagearc-{Guid.NewGuid():N}.epub");
        var bookId = $"test-{Guid.NewGuid():N}";
        try
        {
            using (var archive = ZipFile.Open(epubPath, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "mimetype", "application/epub+zip");
                WriteEntry(archive, "META-INF/container.xml", """
                    <?xml version="1.0"?>
                    <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
                      <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
                    </container>
                    """);
                WriteEntry(archive, "OEBPS/content.opf", """
                    <?xml version="1.0" encoding="utf-8"?>
                    <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="id">
                      <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                        <dc:identifier id="id">pagearc-test</dc:identifier>
                        <dc:title>PageArc EPUB Test</dc:title>
                        <dc:creator>Test Author</dc:creator>
                        <dc:language>en</dc:language>
                      </metadata>
                      <manifest>
                        <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                        <item id="ch1" href="Text/Chapter%201.xhtml" media-type="application/xhtml+xml"/>
                      </manifest>
                      <spine><itemref idref="ch1"/></spine>
                    </package>
                    """);
                WriteEntry(archive, "OEBPS/nav.xhtml", """
                    <?xml version="1.0" encoding="utf-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml"><head><title>TOC</title></head><body>
                    <nav><ol><li><a href="Text/Chapter%201.xhtml#start">Chapter One</a></li></ol></nav>
                    </body></html>
                    """);
                WriteEntry(archive, "OEBPS/Text/Chapter 1.xhtml", """
                    <?xml version="1.0" encoding="utf-8"?>
                    <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Chapter One</title></head>
                    <body><h1 id="start">Chapter One</h1><p>Hello PageArc.</p></body></html>
                    """);
            }

            var metadata = await EpubParser.ReadMetadataAsync(epubPath);
            Assert.Equal("PageArc EPUB Test", metadata.Title);
            Assert.Equal("Test Author", metadata.Author);

            var book = new BookEntry
            {
                Id = bookId,
                FilePath = epubPath,
                Format = "EPUB",
                Title = metadata.Title,
                Author = metadata.Author
            };
            var document = await EpubParser.OpenAsync(book);
            Assert.Single(document.Spine);
            Assert.Equal("OEBPS/Text/Chapter 1.xhtml", document.Spine[0].RelativePath);
            Assert.Single(document.Toc);
            Assert.Equal("OEBPS/Text/Chapter 1.xhtml#start", document.Toc[0].Href);

            var rendered = await EpubWebRenderer.PrepareAsync(document, 0);
            Assert.Equal("__pagearc/spine-0000.html", rendered.WebPath);
            Assert.Contains("<base href=\"https://pagearc.local/OEBPS/Text/Chapter%201.xhtml\">", rendered.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Hello PageArc.", rendered.Html, StringComparison.Ordinal);
            Assert.Contains("Hello PageArc.", rendered.PlainText, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(document.ExtractionRoot, "__pagearc", "spine-0000.html")));
        }
        finally
        {
            if (File.Exists(epubPath)) File.Delete(epubPath);
            var cache = Path.Combine(AppPaths.BooksCacheRoot, bookId);
            if (Directory.Exists(cache)) Directory.Delete(cache, true);
        }
    }

    [Fact]
    public async Task Epub2CalibrePipeline_ParsesRootOpfNcxHtmlSpineAndSvgCover()
    {
        var epubPath = Path.Combine(Path.GetTempPath(), $"pagearc-epub2-{Guid.NewGuid():N}.epub");
        var bookId = $"epub2-{Guid.NewGuid():N}";
        try
        {
            using (var archive = ZipFile.Open(epubPath, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "mimetype", "application/epub+zip");
                WriteEntry(archive, "META-INF/container.xml", """
                    <?xml version="1.0"?>
                    <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                      <rootfiles><rootfile full-path="content.opf" media-type="application/oebps-package+xml"/></rootfiles>
                    </container>
                    """);
                WriteEntry(archive, "content.opf", """
                    <?xml version='1.0' encoding='utf-8'?>
                    <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="uuid_id">
                      <metadata xmlns:opf="http://www.idpf.org/2007/opf" xmlns:dc="http://purl.org/dc/elements/1.1/">
                        <dc:title>Calibre EPUB2 Regression</dc:title>
                        <dc:creator opf:role="aut">Test Author</dc:creator>
                        <dc:identifier id="uuid_id" opf:scheme="uuid">fixture</dc:identifier>
                        <dc:language>zh</dc:language>
                      </metadata>
                      <manifest>
                        <item id="cover" href="cover.jpeg" media-type="image/jpeg"/>
                        <item id="titlepage" href="titlepage.xhtml" media-type="application/xhtml+xml"/>
                        <item id="chapter" href="text/part0000.html" media-type="application/xhtml+xml"/>
                        <item id="css" href="stylesheet.css" media-type="text/css"/>
                        <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/>
                      </manifest>
                      <spine toc="ncx"><itemref idref="titlepage"/><itemref idref="chapter"/></spine>
                    </package>
                    """);
                WriteEntry(archive, "toc.ncx", """
                    <?xml version='1.0' encoding='utf-8'?>
                    <ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
                      <docTitle><text>Calibre EPUB2 Regression</text></docTitle>
                      <navMap>
                        <navPoint id="n1" playOrder="1"><navLabel><text>Chapter</text></navLabel><content src="text/part0000.html#start"/></navPoint>
                      </navMap>
                    </ncx>
                    """);
                WriteEntry(archive, "titlepage.xhtml", """
                    <?xml version='1.0' encoding='utf-8'?>
                    <html xmlns="http://www.w3.org/1999/xhtml"><head><title>Cover</title></head><body>
                      <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 100 160">
                        <image width="100" height="160" xlink:href="cover.jpeg"/>
                      </svg>
                    </body></html>
                    """);
                WriteEntry(archive, "text/part0000.html", """
                    <?xml version='1.0' encoding='utf-8'?>
                    <html xmlns="http://www.w3.org/1999/xhtml"><head><link href="../stylesheet.css" rel="stylesheet" type="text/css"/></head>
                    <body><h1 id="start">Synthetic chapter</h1><p>Readable EPUB2 content.</p></body></html>
                    """);
                WriteEntry(archive, "stylesheet.css", "body { margin: 0; }");
                WriteEntry(archive, "cover.jpeg", "synthetic-fixture");
            }

            var book = new BookEntry { Id = bookId, FilePath = epubPath, Format = "EPUB", Title = "Fixture" };
            var document = await EpubParser.OpenAsync(book);

            Assert.Equal(2, document.Spine.Count);
            Assert.Equal("titlepage.xhtml", document.Spine[0].RelativePath);
            Assert.Equal("text/part0000.html", document.Spine[1].RelativePath);
            Assert.Single(document.Toc);
            Assert.Equal("text/part0000.html#start", document.Toc[0].Href);
            Assert.Equal(1, EpubWebRenderer.ResolveInitialSpineIndex(document, 0, 0));
            Assert.Equal(0, EpubWebRenderer.ResolveInitialSpineIndex(document, 0, 0.5));

            var cover = await EpubWebRenderer.PrepareAsync(document, 0);
            Assert.Contains("href=\"cover.jpeg\"", cover.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("xlink:href", cover.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<base href=\"https://pagearc.local/titlepage.xhtml\">", cover.Html, StringComparison.OrdinalIgnoreCase);

            var chapter = await EpubWebRenderer.PrepareAsync(document, 1);
            Assert.Contains("<base href=\"https://pagearc.local/text/part0000.html\">", chapter.Html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Readable EPUB2 content.", chapter.Html, StringComparison.Ordinal);
            Assert.Contains("Readable EPUB2 content.", chapter.PlainText, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(epubPath)) File.Delete(epubPath);
            var cache = Path.Combine(AppPaths.BooksCacheRoot, bookId);
            if (Directory.Exists(cache)) Directory.Delete(cache, true);
        }
    }

    [Fact]
    public void ResourceFiles_HaveIdenticalKeys()
    {
        var root = FindRepoRoot();
        var paths = new[]
        {
            Path.Combine(root, "Strings", "en-US", "Resources.resw"),
            Path.Combine(root, "Strings", "zh-CN", "Resources.resw"),
            Path.Combine(root, "Strings", "ja-JP", "Resources.resw")
        };
        var sets = paths.Select(ReadKeys).ToArray();
        Assert.True(sets[0].SetEquals(sets[1]), "zh-CN resource keys differ from en-US.");
        Assert.True(sets[0].SetEquals(sets[2]), "ja-JP resource keys differ from en-US.");
    }

    [Fact]
    public void AppManifest_DeclaresPerMonitorV2DpiAwareness()
    {
        var root = FindRepoRoot();
        var manifest = XDocument.Load(Path.Combine(root, "app.manifest"));
        XNamespace dpi = "http://schemas.microsoft.com/SMI/2016/WindowsSettings";
        var awareness = manifest.Descendants(dpi + "dpiAwareness").SingleOrDefault();
        Assert.NotNull(awareness);
        Assert.Contains("PerMonitorV2", awareness!.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellTheme_UsesMicaAndNeutralTitleBarPalette()
    {
        var root = FindRepoRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var mainWindowXaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        var readerXaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));

        Assert.Contains("<MicaBackdrop/>", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("#F3F3F3", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#202020", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#E5F9F9", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#1A2323", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PageArcNavigationPaneBrush", appXaml, StringComparison.Ordinal);
        Assert.Contains("CardBackgroundFillColorDefaultBrush", appXaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"Transparent\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ThemeTransitionOverlay", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"#F9F9F9\"", readerXaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NavigationShell_UsesFigmaAdaptiveMinimalOverlayBehaviorAndTabs()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        Assert.Contains("IsBackButtonVisible=\"Collapsed\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PaneDisplayMode=\"Auto\"", xaml, StringComparison.Ordinal);
        Assert.Contains("OpenPaneLength=\"240\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CompactPaneLength=\"64\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ShellTabs\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsAddTabButtonVisible=\"True\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsPaneOpen=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("NavigationViewDisplayMode.Minimal", code, StringComparison.Ordinal);
        Assert.Contains("sender.IsPaneOpen = false", code, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"Nav_Categories\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"Nav_Conversion\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Symbol=\"Switch\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Symbol=\"SyncFolder\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"Nav_Recent\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"Nav_Favorites\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"Nav_Collections\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Reader_UsesUnifiedFlowHostAndKeepsFigmaShellContract()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml.cs"));
        Assert.Contains("<WebView2 x:Name=\"ReaderWebView\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FlowReaderEngine", code, StringComparison.Ordinal);
        Assert.Contains("IFlowBookSource", code, StringComparison.Ordinal);
        Assert.Contains("SetVirtualHostNameToFolderMapping", code, StringComparison.Ordinal);
        Assert.Contains("WebResourceRequested", code, StringComparison.Ordinal);
        Assert.Contains("SectionFraction", code, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinition x:Name=\"ContentsColumn\" Width=\"260\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"760\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("This page contains no text", code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LanguageSwitch_AlwaysReEnablesSelectorAndKeepsWindowInPlace()
    {
        var root = FindRepoRoot();
        var settingsCode = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml.cs"));
        var windowCode = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        Assert.Contains("finally", settingsCode, StringComparison.Ordinal);
        Assert.Contains("LanguageCombo.IsEnabled = true;", settingsCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ReloadMainWindow", settingsCode, StringComparison.Ordinal);
        Assert.Contains("ReloadLocalizedShell", windowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeSwitch_UsesDirectRequestedThemeWithoutFullWindowCrossfade()
    {
        var root = FindRepoRoot();
        var settingsCode = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml.cs"));
        var windowCode = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var windowXaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        Assert.Contains("App.MainWindow?.ApplyAppTheme(tag);", settingsCode, StringComparison.Ordinal);
        Assert.Contains("public void ApplyAppTheme(string theme)", windowCode, StringComparison.Ordinal);
        Assert.Contains("RootGrid.RequestedTheme", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("BeginThemeTransition", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ThemeTransitionOverlay", windowXaml, StringComparison.Ordinal);
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static SortedSet<string> ReadKeys(string path) =>
        new(XDocument.Load(path).Root!
            .Elements("data")
            .Select(element => element.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!));

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "PageArc.csproj"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("PageArc repository root not found.");
    }
}