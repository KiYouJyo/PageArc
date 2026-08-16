using Xunit;

namespace PageArc.Tests;

public sealed class NavigationPaneStateTests
{
    [Fact]
    public void NavigationPane_UsesNeutralInactiveAndCyanActiveWindowStates()
    {
        var root = FindRepoRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var windowCode = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));

        Assert.Contains("#F3F3F3", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#202020", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PageArcNavigationPaneRestBrush", appXaml, StringComparison.Ordinal);

        Assert.Contains("Activated += MainWindow_Activated", windowCode, StringComparison.Ordinal);
        Assert.Contains("WindowActivationState.Deactivated", windowCode, StringComparison.Ordinal);
        Assert.Contains("_isWindowActive", windowCode, StringComparison.Ordinal);
        Assert.Contains("ColorHelper.FromArgb(255, 26, 35, 35)", windowCode, StringComparison.Ordinal);
        Assert.Contains("ColorHelper.FromArgb(255, 229, 249, 249)", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("forceActive", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("AppNavigation.IsPaneOpen || AppNavigation.DisplayMode == NavigationViewDisplayMode.Expanded", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("PaneOpening +=", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("PaneClosed +=", windowCode, StringComparison.Ordinal);
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
