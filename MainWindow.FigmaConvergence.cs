using Microsoft.UI.Xaml;
using PageArc.Services;

namespace PageArc;

public sealed partial class MainWindow
{
    private void AppNavigation_Loaded(object sender, RoutedEventArgs e)
    {
        // The previous XAML fallback values are kept for compatibility with the adaptive
        // shell contract; once the real NavigationView is loaded, use the exact PAGEARC
        // desktop geometry measured from Figma nodes 16:3 and 16:346.
        AppNavigation.OpenPaneLength = 240;
        AppNavigation.CompactPaneLength = 64;

        // RootGrid can finish loading before NavigationView has materialized its internal
        // SplitView.  Refresh again from the control's own Loaded event so a persisted Light
        // theme cannot leave the initial pane using the process/system Dark brush.  Reset the
        // cached template part in case WinUI recreated the template during startup.
        _navigationSplitView = null;
        ApplyNavigationPaneBackground();

        // Loaded can precede NavigationView's final template layout. Synchronize once more
        // from the first stable LayoutUpdated notification, then detach instead of keeping a
        // permanent per-frame workaround.
        if (!_navigationPaneStartupLayoutPending)
        {
            _navigationPaneStartupLayoutPending = true;
            AppNavigation.LayoutUpdated += AppNavigation_StartupLayoutUpdated;
        }
    }

    private void AppNavigation_StartupLayoutUpdated(object? sender, object e)
    {
        if (!_navigationPaneStartupLayoutPending) return;
        _navigationPaneStartupLayoutPending = false;
        AppNavigation.LayoutUpdated -= AppNavigation_StartupLayoutUpdated;
        _navigationSplitView = null;
        ApplyNavigationPaneBackground();

        StartupDiagnostics.Log(
            $"Navigation pane startup settled: requested={_requestedAppTheme}; " +
            $"rootActual={RootGrid.ActualTheme}; navigationActual={AppNavigation.ActualTheme}; " +
            $"splitViewFound={_navigationSplitView is not null}.");
    }
}
