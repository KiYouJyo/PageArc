using PageArc.Models;
using PageArc.Services;
using System.IO.Compression;
using System.Xml.Linq;
using Xunit;

namespace PageArc.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void AppSettings_DefaultsAreLocalFirst()
    {
        var settings = new AppSettings();
        Assert.Equal("system", settings.AppTheme);
        Assert.Equal("system", settings.Language);
        Assert.Equal("light", settings.ReadingTheme);
        Assert.True(settings.ReadingThemeFollowsApp);
        Assert.True(settings.DetectDuplicates);
    }

    [Fact]
    public void BookFormatRegistry_ContainsTheFivePageArcFormats()
    {
        Assert.Equal(["EPUB", "FB2", "MOBI", "AZW3", "LIT"], BookFormatRegistry.RequiredFormats.Select(x => x.Id).ToArray());
    }

    [Fact]
    public void LibraryImportResult_TracksOutcomes()
    {
        var result = new LibraryImportResult();
        result.Items.Add(new LibraryImportItem("a.epub", LibraryImportOutcome.Added));
        result.Items.Add(new LibraryImportItem("b.epub", LibraryImportOutcome.Skipped));
        result.Items.Add(new LibraryImportItem("c.epub", LibraryImportOutcome.Error, "bad"));
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(1, result.ErrorCount);
    }

    [Fact]
    public void SettingsService_RoundTrips()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "settings.json");
            var settings = new SettingsService(file);
            settings.Load();
            settings.Update(value =>
            {
                value.AppTheme = "dark";
                value.Language = "ja-JP";
            });
            var reloaded = new SettingsService(file);
            reloaded.Load();
            Assert.Equal("dark", reloaded.Current.AppTheme);
            Assert.Equal("ja-JP", reloaded.Current.Language);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CategoryService_RoundTripsMembership()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-category-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var service = new CategoryService(Path.Combine(root, "categories.json"));
            service.Load();
            var category = service.Create("Urbanism");
            service.AddBook(category.Id, "book-1");

            var reloaded = new CategoryService(Path.Combine(root, "categories.json"));
            reloaded.Load();
            Assert.Contains("book-1", Assert.Single(reloaded.Categories).BookIds);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CacheMaintenance_DoesNotDeletePersistentData()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-cache-{Guid.NewGuid():N}");
        var cache = Path.Combine(root, "Cache");
        var persistent = Path.Combine(root, "library.json");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "generated.tmp"), "cache");
        File.WriteAllText(persistent, "library");
        try
        {
            var service = new CacheMaintenanceService(cache);
            service.ClearGeneratedCache();
            Assert.True(File.Exists(persistent));
            Assert.Empty(Directory.EnumerateFileSystemEntries(cache));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void VersionParser_OrdersSemver()
    {
        Assert.True(VersionParser.TryParse("v1.2.3", out var current));
        Assert.True(VersionParser.TryParse("1.3.0", out var newer));
        Assert.True(newer > current);
    }

    [Fact]
    public void Resources_ContainCoreNavigationLabels()
    {
        var root = FindRepoRoot();
        foreach (var language in new[] { "zh-CN", "ja-JP", "en-US" })
        {
            var document = XDocument.Load(Path.Combine(root, "Strings", language, "Resources.resw"));
            var names = document.Descendants("data").Select(x => x.Attribute("name")?.Value).ToHashSet();
            Assert.Contains("Nav_Library.Content", names);
            Assert.Contains("Nav_Categories.Content", names);
            Assert.Contains("Nav_Conversion.Content", names);
        }
    }

    [Fact]
    public void Manifest_AssociatesSupportedEbookFormats()
    {
        var root = FindRepoRoot();
        var document = XDocument.Load(Path.Combine(root, "Packaging", "PageArc.Package.appxmanifest"));
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
        var extensions = document.Descendants(uap + "FileType").Select(x => x.Value.ToLowerInvariant()).ToHashSet();
        foreach (var extension in new[] { ".epub", ".fb2", ".mobi", ".azw", ".azw3", ".lit" })
            Assert.Contains(extension, extensions);
    }

    [Fact]
    public void ThirdPartyFoliateFiles_ArePresentAndPinned()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "ThirdParty", "foliate-js", "mobi.js")));
        Assert.True(File.Exists(Path.Combine(root, "ThirdParty", "foliate-js", "vendor", "fflate.js")));
        var pin = File.ReadAllText(Path.Combine(root, "ThirdParty", "foliate-js", "PIN.md"));
        Assert.Contains("78914aef4466eb960965702401634c2cb348e9b1", pin, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FigmaSurfaceContract_IsPresent()
    {
        Assert.NotEmpty(FigmaSurfaceContract.Surfaces);
    }

    [Fact]
    public void ReaderDataBackup_RoundTrips()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var sourceFile = Path.Combine(root, "reading-data.json");
            var source = new ReadingDataService(sourceFile);
            source.Load();
            source.SetPosition("book", new FlowContentLocator(2, 0.4), 0.6);
            source.ToggleBookmark("book", new FlowContentLocator(2, 0.4), "chapter", "quote");
            var backup = new ReadingBackupService(source);
            var zip = Path.Combine(root, "backup.zip");
            backup.Export(zip);

            var targetFile = Path.Combine(root, "restored.json");
            var target = new ReadingDataService(targetFile);
            target.Load();
            new ReadingBackupService(target).Import(zip);
            var restored = target.GetPosition("book");
            Assert.NotNull(restored);
            Assert.Equal(2, restored!.Locator.SectionIndex);
            Assert.Single(target.GetBookmarks("book"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReaderBackupZip_ContainsVersionedJson()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-backup-zip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var data = new ReadingDataService(Path.Combine(root, "reading-data.json"));
            data.Load();
            var zip = Path.Combine(root, "backup.zip");
            new ReadingBackupService(data).Export(zip);
            using var archive = ZipFile.OpenRead(zip);
            Assert.Contains(archive.Entries, entry => entry.FullName.EndsWith("reading-data.json", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, true);
        }
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
    public void NavigationShell_UsesFigmaAdaptivePaneAndTabbedTitleBar()
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
    public void ReaderData_LocatorRoundTrips()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-reading-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "reading.json");
            var data = new ReadingDataService(file);
            data.Load();
            data.SetPosition("book", new FlowContentLocator(4, 0.33), 0.8);
            var result = data.GetPosition("book");
            Assert.NotNull(result);
            Assert.Equal(4, result!.Locator.SectionIndex);
            Assert.Equal(0.33, result.Locator.Fraction, 3);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PageArc.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate PageArc repository root.");
    }
}
