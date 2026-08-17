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
    public void LanguageSwitchKeepsExistingWindowAndReloadsOnlyLocalizedContent()
    {
        var root = FindRepoRoot();
        var settingsCode = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml.cs"));
        var windowCode = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var localizationCode = File.ReadAllText(Path.Combine(root, "Services", "LocalizationService.cs"));

        Assert.DoesNotContain("ReloadMainWindow", settingsCode, StringComparison.Ordinal);
        Assert.Contains("LanguageChanged", localizationCode, StringComparison.Ordinal);
        Assert.Contains("App.Localization.LanguageChanged += OnLanguageChanged", windowCode, StringComparison.Ordinal);
        Assert.Contains("ReloadLocalizedShell", windowCode, StringComparison.Ordinal);
        Assert.Contains("SuppressNavigationTransitionInfo", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("new MainWindow", settingsCode, StringComparison.Ordinal);
    }

    [Fact]
    public void BookmarkToolbarEntryOnlyOpensTheBookmarksPane()
    {
        var root = FindRepoRoot();
        var figmaCode = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.Figma.cs"));

        Assert.Contains("BookmarkButton.Click -= Bookmark_Click", figmaCode, StringComparison.Ordinal);
        Assert.Contains("BookmarkButton.Click += OpenBookmarksPane_Click", figmaCode, StringComparison.Ordinal);
        Assert.Contains("ShowSidebar(ReaderSidebarMode.Bookmarks)", figmaCode, StringComparison.Ordinal);

        var start = figmaCode.IndexOf("private void OpenBookmarksPane_Click", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = figmaCode.IndexOf("private async void ReaderRootGrid_ActualThemeChanged", start, StringComparison.Ordinal);
        Assert.True(end > start);
        var handler = figmaCode[start..end];
        Assert.DoesNotContain("ToggleBookmark", handler, StringComparison.Ordinal);
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
