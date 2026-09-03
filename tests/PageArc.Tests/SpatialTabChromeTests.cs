using Xunit;

namespace PageArc.Tests;

public sealed class SpatialTabChromeTests
{
    [Fact]
    public void TitleBar_UsesApprovedSpatialViewerGeometryAndAdaptiveWidths()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        var chrome = File.ReadAllText(Path.Combine(root, "MainWindow.SpatialTabChrome.cs"));

        Assert.Contains("Background=\"Transparent\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"104,*,132\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Padding=\"16,0,12,0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"32\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SpatialPreferredTabWidth = 220", chrome, StringComparison.Ordinal);
        Assert.Contains("SpatialMinimumTabWidth = 72", chrome, StringComparison.Ordinal);
        Assert.Contains("SpatialNewTabButtonWidth = 32", chrome, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp", chrome, StringComparison.Ordinal);
        Assert.Contains("AppTitleBar.SizeChanged", chrome, StringComparison.Ordinal);
        Assert.Contains("RepositionThemeTransition", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void TitleBar_UsesSpatialViewerLightDarkAndStaticTabStates()
    {
        var root = FindRepoRoot();
        var chrome = File.ReadAllText(Path.Combine(root, "MainWindow.SpatialTabChrome.cs"));

        Assert.Contains("TitleBarTheme.Dark", chrome, StringComparison.Ordinal);
        Assert.Contains("TitleBarTheme.Light", chrome, StringComparison.Ordinal);
        Assert.Contains("ColorHelper.FromArgb(32, 255, 255, 255)", chrome, StringComparison.Ordinal);
        Assert.Contains("ColorHelper.FromArgb(24, 0, 0, 0)", chrome, StringComparison.Ordinal);
        Assert.Contains("ColorHelper.FromArgb(48, 255, 255, 255)", chrome, StringComparison.Ordinal);
        Assert.Contains("ColorHelper.FromArgb(40, 0, 0, 0)", chrome, StringComparison.Ordinal);
        Assert.Contains("compositionVisual.StopAnimation(\"Opacity\")", chrome, StringComparison.Ordinal);
        Assert.Contains("visual.HeaderText.Opacity = 1", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void NavigationPane_SharesTitleBarMicaWithoutExtraTintLayer()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        var resources = File.ReadAllText(Path.Combine(root, "App.xaml"));

        Assert.Contains("ResourceKey=\"PageArcNavigationPaneBrush\"", xaml, StringComparison.Ordinal);
        Assert.Contains("NavigationViewBorderThickness\">0", xaml, StringComparison.Ordinal);
        Assert.Contains("NavigationViewPaneContentGridMargin\">0", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"{ThemeResource PageArcToolbarBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"PageArcNavigationPaneBrush\" Color=\"Transparent\"", resources, StringComparison.Ordinal);
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
