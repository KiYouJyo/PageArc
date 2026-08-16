using System.Xml.Linq;
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
        const string source = "<?xml version=\"1.0\" encoding=\"utf-8\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>X</title></head><body><img src=\"images/a.png\"/></body></html>";
        var html = EpubWebRenderer.NormalizeForWebView(source, "https://pagearc.local/OEBPS/Text/");

        Assert.DoesNotContain("<?xml", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<meta charset=\"utf-8\">", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<base href=\"https://pagearc.local/OEBPS/Text/\">", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("images/a.png", html, StringComparison.Ordinal);
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
    public void ShellThemeResources_AreDynamicGradientsForLightAndDarkModes()
    {
        var root = FindRepoRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var mainWindowXaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        var readerXaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));

        Assert.Contains("ResourceDictionary.ThemeDictionaries", appXaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"Light\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"Dark\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("LinearGradientBrush x:Key=\"PageArcCanvasBrush\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("#102A2E", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#F8FCFB", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{ThemeResource PageArcCardBrush}", appXaml, StringComparison.Ordinal);
        Assert.Contains("{ThemeResource PageArcCanvasBrush}", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"#F6F6F6\"", mainWindowXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Background=\"#F9F9F9\"", readerXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{ThemeResource PageArcToolbarBrush}", readerXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void NavigationShell_HidesItsImplicitBackButtonAndUsesFigmaInformationArchitecture()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        Assert.Contains("IsBackButtonVisible=\"Collapsed\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"Nav_Categories\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Uid=\"Nav_Conversion\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"Nav_Recent\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"Nav_Favorites\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Uid=\"Nav_Collections\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeSwitch_UsesLiveWindowApplicationWithCrossfade()
    {
        var root = FindRepoRoot();
        var settingsCode = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml.cs"));
        var windowCode = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var windowXaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        Assert.Contains("App.MainWindow?.ApplyAppTheme(tag);", settingsCode, StringComparison.Ordinal);
        Assert.Contains("public void ApplyAppTheme(string theme)", windowCode, StringComparison.Ordinal);
        Assert.Contains("BeginThemeTransition", windowCode, StringComparison.Ordinal);
        Assert.Contains("ThemeTransitionOverlay", windowXaml, StringComparison.Ordinal);
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
