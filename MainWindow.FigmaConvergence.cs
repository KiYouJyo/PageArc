using Microsoft.UI.Xaml;

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
        QueueNavigationPaneBackgroundUpdate();
    }
}
