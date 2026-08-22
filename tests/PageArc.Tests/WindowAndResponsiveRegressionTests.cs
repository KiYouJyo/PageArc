using Xunit;

namespace PageArc.Tests;

public sealed class WindowAndResponsiveRegressionTests
{
    [Fact]
    public void WindowPlacementIsRestoredClampedAndSavedAtClose()
    {
        var root = FindRepoRoot();
        var service = File.ReadAllText(Path.Combine(root, "Services", "WindowPlacementService.cs"));
        var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var app = File.ReadAllText(Path.Combine(root, "App.xaml.cs"));

        Assert.Contains("DisplayArea.GetFromRect", service, StringComparison.Ordinal);
        Assert.Contains("DisplayAreaFallback.Nearest", service, StringComparison.Ordinal);
        Assert.Contains("OverlappedPresenterState.Minimized", service, StringComparison.Ordinal);
        Assert.Contains("presenter.Maximize()", service, StringComparison.Ordinal);
        Assert.Contains("_windowPlacementService.Restore();", window, StringComparison.Ordinal);
        Assert.Contains("_windowPlacementService.Save();", window, StringComparison.Ordinal);
        Assert.Contains("previous?.SaveWindowPlacement();", app, StringComparison.Ordinal);
    }

    [Fact]
    public void LibraryAndLightThemeUseResponsiveNonOverlayLayout()
    {
        var root = FindRepoRoot();
        var library = File.ReadAllText(Path.Combine(root, "Pages", "LibraryPage.xaml"));
        var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var appXaml = File.ReadAllText(Path.Combine(root, "App.xaml"));

        Assert.Contains("x:Name=\"LibraryWidthStates\"", library, StringComparison.Ordinal);
        Assert.Contains("AdaptiveTrigger MinWindowWidth=\"1200\"", library, StringComparison.Ordinal);
        Assert.Contains("HeaderActions.(Grid.Row)", library, StringComparison.Ordinal);
        Assert.Contains("NavigationViewPaneDisplayMode.Left", window, StringComparison.Ordinal);
        Assert.Contains("NavigationViewPaneDisplayMode.LeftCompact", window, StringComparison.Ordinal);
        Assert.Contains("PageArcWindowBackgroundBrush", appXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("229, 249, 249", window, StringComparison.Ordinal);
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
