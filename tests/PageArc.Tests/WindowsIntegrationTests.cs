using System.Xml.Linq;
using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class WindowsIntegrationTests
{
    [Theory]
    [InlineData(@"C:\Books\Example.epub", "EPUB")]
    [InlineData(@"C:\Books\Example.fb2", "FB2")]
    [InlineData(@"C:\Books\Example.mobi", "MOBI")]
    [InlineData(@"C:\Books\Example.azw", "MOBI")]
    [InlineData(@"C:\Books\Example.azw3", "AZW3")]
    [InlineData(@"C:\Books\Example.lit", "LIT")]
    public void LaunchArguments_ParseAllAssociatedEbookExtensions(string path, string expectedFormat)
    {
        var request = AppActivationRequestParser.FromLaunchArguments($"\"{path}\"");
        Assert.Equal(AppActivationRequestKind.Files, request.Kind);
        var parsed = Assert.Single(request.FilePaths);
        Assert.Equal(expectedFormat, BookFormatRegistry.FormatFromPath(parsed));
    }

    [Fact]
    public void LaunchArguments_PreserveQuotedPathsWithSpaces()
    {
        const string path = @"C:\My Books\A Great Book.epub";
        var request = AppActivationRequestParser.FromLaunchArguments($"\"{path}\"");
        Assert.Equal(AppActivationRequestKind.Files, request.Kind);
        Assert.EndsWith(@"My Books\A Great Book.epub", Assert.Single(request.FilePaths), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BookProtocol_RoundTripsStableLibraryId()
    {
        const string id = "book id/with symbols";
        var uri = AppActivationRequestParser.CreateBookUri(id);
        var request = AppActivationRequestParser.FromProtocol(uri);
        Assert.Equal(AppActivationRequestKind.Book, request.Kind);
        Assert.Equal(id, request.BookId);
    }

    [Fact]
    public void OpenProtocol_ParsesEncodedBookId()
    {
        var request = AppActivationRequestParser.FromProtocol(new Uri("pagearc://open?book=abc%20123"));
        Assert.Equal(AppActivationRequestKind.Book, request.Kind);
        Assert.Equal("abc 123", request.BookId);
    }

    [Fact]
    public void PackageManifest_DeclaresAllRequiredAssociationsAndProtocol()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Packaging", "PageArc.Package.appxmanifest");
        Assert.True(File.Exists(path), path);
        var document = XDocument.Load(path);
        var fileTypes = document.Descendants()
            .Where(x => x.Name.LocalName == "FileType")
            .Select(x => x.Value.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var extension in new[] { ".epub", ".fb2", ".mobi", ".azw", ".azw3", ".lit" })
            Assert.Contains(extension, fileTypes);

        var protocols = document.Descendants()
            .Where(x => x.Name.LocalName == "Protocol")
            .Select(x => (string?)x.Attribute("Name"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        Assert.Contains(protocols, x => string.Equals(x, "pagearc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AppBranding_UsesTheProvidedLogoAcrossWindowsAndAbout()
    {
        var root = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "PageArc.csproj"));
        var about = File.ReadAllText(Path.Combine(root, "Pages", "AboutPage.xaml"));
        var manifest = XDocument.Load(Path.Combine(root, "Package.appxmanifest"));

        Assert.Contains("<ApplicationIcon>Assets\\AppIcon.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains("ms-appx:///Assets/Icon-Large-1024.png", about, StringComparison.Ordinal);
        Assert.Contains(manifest.Descendants(), element =>
            element.Name.LocalName == "FileTypeAssociation"
            && element.Elements().Any(child => child.Name.LocalName == "Logo"
                && child.Value.Trim().EndsWith("Square44x44Logo.png", StringComparison.OrdinalIgnoreCase)));

        foreach (var asset in new[]
                 {
                     "AppIcon.ico", "AppLogo.png", "Square44x44Logo.png",
                     "Square150x150Logo.png", "StoreLogo.png", "Wide310x150Logo.png"
                 })
        {
            var path = Path.Combine(root, "Assets", asset);
            Assert.True(File.Exists(path), path);
            Assert.True(new FileInfo(path).Length > 0, path);
        }
    }

    [Fact]
    public void ShellBrandingProvidesSharpUnplatedCandidatesAndRuntimeWindowIcon()
    {
        var root = FindRepoRoot();
        var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var manifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));

        Assert.Contains("AppWindow.SetIcon", window, StringComparison.Ordinal);
        Assert.Contains("\"Assets\", \"AppIcon.ico\"", window, StringComparison.Ordinal);
        Assert.Contains("AppIcon.ico", window, StringComparison.Ordinal);
        Assert.Contains("BackgroundColor=\"transparent\"", manifest, StringComparison.Ordinal);

        foreach (var scale in new[] { 100, 125, 150, 200, 400 })
        {
            Assert.True(File.Exists(Path.Combine(root, "Assets", $"Square44x44Logo.scale-{scale}.png")));
            Assert.True(File.Exists(Path.Combine(root, "Assets", $"Square150x150Logo.scale-{scale}.png")));
            Assert.True(File.Exists(Path.Combine(root, "Assets", $"StoreLogo.scale-{scale}.png")));
        }

        foreach (var size in new[] { 16, 20, 24, 30, 32, 36, 40, 44, 48, 60, 64, 72, 80, 96, 256 })
        {
            Assert.True(File.Exists(Path.Combine(root, "Assets", $"Square44x44Logo.targetsize-{size}_altform-unplated.png")));
            Assert.True(File.Exists(Path.Combine(root, "Assets", $"Square44x44Logo.targetsize-{size}_altform-lightunplated.png")));
        }
    }

    [Fact]
    public void StartupOverlayMatchesTheUrbanPlanToolboxReadinessAndFadePattern()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var manifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));

        Assert.Contains("MainContent\" Opacity=\"0\" IsHitTestVisible=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("StartupOverlay\" Background=\"Transparent\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Icon-Large-1024.png", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"183\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"183\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CompositionTarget.Rendering", code, StringComparison.Ordinal);
        Assert.Contains("MainContent.Opacity = 1", code, StringComparison.Ordinal);
        Assert.Contains("StartupSplashTiming.FadeOutDuration", code, StringComparison.Ordinal);
        Assert.Contains("CubicEase { EasingMode = EasingMode.EaseOut }", code, StringComparison.Ordinal);
        Assert.Contains("StartupWatchdogTriggered", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", code, StringComparison.Ordinal);
        Assert.Contains("uap5:Optional=\"true\"", manifest, StringComparison.Ordinal);
        foreach (var scale in new[] { 100, 125, 150, 200, 400 })
            Assert.True(File.Exists(Path.Combine(root, "Assets", $"SplashScreen.scale-{scale}.png")));
    }

    [Fact]
    public void LocalizationIsEstablishedBeforeXamlAndPackagesEverySupportedLanguage()
    {
        var root = FindRepoRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml.cs"));
        var manifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        var localizationIndex = app.IndexOf("Localization.ApplyPersistedLanguage(Settings.Current);", StringComparison.Ordinal);
        var xamlIndex = app.IndexOf("InitializeComponent();", StringComparison.Ordinal);

        Assert.True(localizationIndex >= 0 && localizationIndex < xamlIndex);
        Assert.Contains("<Resource Language=\"zh-CN\"", manifest, StringComparison.Ordinal);
        Assert.Contains("<Resource Language=\"ja-JP\"", manifest, StringComparison.Ordinal);
        Assert.Contains("<Resource Language=\"en-US\"", manifest, StringComparison.Ordinal);
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
