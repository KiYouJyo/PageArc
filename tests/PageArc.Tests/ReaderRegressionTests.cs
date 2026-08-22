using Xunit;

namespace PageArc.Tests;

public sealed class ReaderRegressionTests
{
    [Fact]
    public void ReaderUsesUnifiedFlowWebViewHostWithSafetyGuards()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml.cs"));

        Assert.Contains("<WebView2 x:Name=\"ReaderWebView\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FlowReaderEngine", code, StringComparison.Ordinal);
        Assert.Contains("EnsureCoreWebView2Async", code, StringComparison.Ordinal);
        Assert.Contains("WebResourceRequested", code, StringComparison.Ordinal);
        Assert.Contains("NewWindowRequested", code, StringComparison.Ordinal);
        Assert.Contains("pagearc.local", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("window.__pagearc", code, StringComparison.Ordinal);
        Assert.Contains("SectionFraction", code, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderNormalizesCalibreEpub2SvgCoverLinksAndExtractsText()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Services", "EpubWebRenderer.cs"));
        Assert.Contains("xlink:href", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=", code, StringComparison.Ordinal);
        Assert.Contains("ExtractReadableText", code, StringComparison.Ordinal);
        Assert.Contains("ResolveInitialSpineIndex", code, StringComparison.Ordinal);
        Assert.Contains("<script", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("javascript:", code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LanguageSwitchRebuildsTheXamlTreeAfterUpdatingTheLanguageContext()
    {
        var root = FindRepoRoot();
        var settingsCode = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml.cs"));
        var windowCode = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var localizationCode = File.ReadAllText(Path.Combine(root, "Services", "LocalizationService.cs"));

        Assert.DoesNotContain("new MainWindow", settingsCode, StringComparison.Ordinal);
        Assert.Contains("LanguageChanged", localizationCode, StringComparison.Ordinal);
        Assert.Contains("App.Localization.LanguageChanged += OnLanguageChanged", windowCode, StringComparison.Ordinal);
        Assert.Contains("App.ReloadMainWindow(App.PendingNavigationTag)", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ReloadLocalizedShell", windowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void BookmarkSidebarNavigationAndBookmarkCreationAreSeparateActions()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));
        var tabbedCode = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.Tabbed.cs"));

        Assert.Contains("x:Name=\"BookmarksModeButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"bookmarks\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AddCurrentBookmarkButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"AddCurrentBookmark_Click\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"BookmarkButton\"", xaml, StringComparison.Ordinal);

        var modeStart = tabbedCode.IndexOf("private void SidebarModeButton_Click", StringComparison.Ordinal);
        var modeEnd = tabbedCode.IndexOf("private void ShowUnifiedSidebar", modeStart, StringComparison.Ordinal);
        Assert.True(modeStart >= 0 && modeEnd > modeStart);
        Assert.DoesNotContain("ToggleBookmark", tabbedCode[modeStart..modeEnd], StringComparison.Ordinal);

        var addStart = tabbedCode.IndexOf("private void AddCurrentBookmark_Click", StringComparison.Ordinal);
        Assert.True(addStart >= 0);
        Assert.Contains("ToggleBookmark", tabbedCode[addStart..], StringComparison.Ordinal);
    }

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
