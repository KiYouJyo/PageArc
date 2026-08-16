using Xunit;

namespace PageArc.Tests;

public sealed class NavigationPaneStateTests
{
    [Fact]
    public void NavigationPane_UsesNeutralRestAndCyanOpenStates()
    {
        var root = FindRepoRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var windowCode = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));

        Assert.Contains("#F3F3F3", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#202020", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PageArcNavigationPaneBrush", appXaml, StringComparison.Ordinal);

        Assert.Contains("PaneOpening", windowCode, StringComparison.Ordinal);
        Assert.Contains("PaneOpened", windowCode, StringComparison.Ordinal);
        Assert.Contains("PaneClosed", windowCode, StringComparison.Ordinal);
        Assert.Contains("forceActive: true", windowCode, StringComparison.Ordinal);
        Assert.Contains("forceActive: false", windowCode, StringComparison.Ordinal);
        Assert.Contains("ColorHelper.FromArgb(255, 26, 35, 35)", windowCode, StringComparison.Ordinal);
        Assert.Contains("ColorHelper.FromArgb(255, 229, 249, 249)", windowCode, StringComparison.Ordinal);
        Assert.Contains("AppNavigation.IsPaneOpen || AppNavigation.DisplayMode == NavigationViewDisplayMode.Expanded", windowCode, StringComparison.Ordinal);
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
