using Microsoft.UI.Xaml;
using PageArc.Services;

namespace PageArc;

public sealed partial class MainWindow
{
    private bool _navigationPaneBackgroundHooked;

    private void AppNavigation_Loaded(object sender, RoutedEventArgs e)
    {
        // Match SpatialViewer's current shell geometry. More importantly, keep the
        // NavigationView lifecycle native: PaneDisplayMode="Auto" decides when the
        // pane is expanded, compact or overlaid, and a user collapse is never undone
        // by a window SizeChanged handler.
        AppNavigation.OpenPaneLength = 252;
        AppNavigation.CompactPaneLength = 64;

        if (!_navigationPaneBackgroundHooked)
        {
            _navigationPaneBackgroundHooked = true;
            AppNavigation.PaneOpening += (_, _) => QueueNavigationPaneBackgroundUpdate();
        }

        // RootGrid can finish loading before NavigationView has materialized its internal
        // SplitView. Refresh from the control's own Loaded event and once more on the next
        // dispatcher turn, mirroring SpatialViewer without mutating IsPaneOpen.
        _navigationSplitView = null;
        ApplyNavigationPaneBackground();
        DispatcherQueue.TryEnqueue(() =>
        {
            _navigationSplitView = null;
            ApplyNavigationPaneBackground();
        });

        // Keep the existing one-shot template-settle guard for cold startup/theme restore.
        // It only synchronizes the transparent Mica pane brush; it never changes pane state.
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
