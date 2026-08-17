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
    }
}
