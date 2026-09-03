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
    public void LibraryAndNavigationUseSpatialViewerNativeResponsiveLifecycle()
    {
        var root = FindRepoRoot();
        var library = File.ReadAllText(Path.Combine(root, "Pages", "LibraryPage.xaml"));
        var mainWindowXaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        var convergence = File.ReadAllText(Path.Combine(root, "MainWindow.FigmaConvergence.cs"));
        var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var appXaml = File.ReadAllText(Path.Combine(root, "App.xaml"));

        Assert.Contains("x:Name=\"LibraryWidthStates\"", library, StringComparison.Ordinal);
        Assert.Contains("AdaptiveTrigger MinWindowWidth=\"1200\"", library, StringComparison.Ordinal);
        Assert.Contains("HeaderActions.(Grid.Row)", library, StringComparison.Ordinal);

        // SpatialViewer lets NavigationView.Auto own the hamburger state. PageArc must
        // not reopen/collapse the pane from a SizeChanged or DisplayModeChanged callback.
        Assert.Contains("PaneDisplayMode=\"Auto\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("OpenPaneLength=\"252\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("CompactPaneLength=\"64\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SizeChanged=\"WorkspaceHost_SizeChanged\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DisplayModeChanged=\"AppNavigation_DisplayModeChanged\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("AppNavigation.OpenPaneLength = 252", convergence, StringComparison.Ordinal);
        Assert.Contains("AppNavigation.CompactPaneLength = 64", convergence, StringComparison.Ordinal);
        Assert.Contains("AppNavigation.PaneOpening", convergence, StringComparison.Ordinal);
        Assert.DoesNotContain("AppNavigation.IsPaneOpen", convergence, StringComparison.Ordinal);
        Assert.DoesNotContain("AppNavigation.PaneDisplayMode", convergence, StringComparison.Ordinal);

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