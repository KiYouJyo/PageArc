using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;

namespace PageArc;

public sealed partial class MainWindow
{
    private const double SpatialPreferredTabWidth = 220;
    private const double SpatialMinimumTabWidth = 72;
    private const double SpatialShellTabSpacing = 8;
    private const double SpatialNewTabButtonWidth = 32;
    private bool _spatialTabChromeInitialized;

    private void AppTitleBar_TabChromeLoaded(object sender, RoutedEventArgs e)
    {
        if (_spatialTabChromeInitialized) return;
        _spatialTabChromeInitialized = true;

        if (AppWindowTitleBar.IsCustomizationSupported())
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        AppTitleBar.SizeChanged += AppTitleBar_SpatialSizeChanged;
        ShellTabItems.LayoutUpdated += ShellTabItems_SpatialLayoutUpdated;
        RootGrid.ActualThemeChanged += RootGrid_SpatialTabChromeThemeChanged;
        Activated += MainWindow_SpatialTabChromeActivated;
        Closed += MainWindow_SpatialTabChromeClosed;

        ApplySpatialTitleBarColors();
        ApplySpatialTabChrome();
    }

    private void AppTitleBar_SpatialSizeChanged(object sender, SizeChangedEventArgs e) => ApplySpatialTabChrome();

    private void ShellTabItems_SpatialLayoutUpdated(object? sender, object e) => ApplySpatialTabChrome();

    private void RootGrid_SpatialTabChromeThemeChanged(FrameworkElement sender, object args) =>
        ApplySpatialTitleBarColors();

    private void MainWindow_SpatialTabChromeActivated(object sender, WindowActivatedEventArgs args) =>
        ApplySpatialTitleBarColors();

    private void MainWindow_SpatialTabChromeClosed(object sender, WindowEventArgs args)
    {
        AppTitleBar.SizeChanged -= AppTitleBar_SpatialSizeChanged;
        ShellTabItems.LayoutUpdated -= ShellTabItems_SpatialLayoutUpdated;
        RootGrid.ActualThemeChanged -= RootGrid_SpatialTabChromeThemeChanged;
        Activated -= MainWindow_SpatialTabChromeActivated;
        Closed -= MainWindow_SpatialTabChromeClosed;
    }

    private void ApplySpatialTabChrome()
    {
        var tabCount = ShellTabItems.Children.Count;
        if (tabCount <= 0) return;

        var titleBarWidth = AppTitleBar.ActualWidth;
        if (!double.IsFinite(titleBarWidth) || titleBarWidth <= 0) return;

        // SpatialViewer v0.3.4 geometry: 104 DIP wordmark column, 132 DIP caption
        // reserve, 16/12 DIP outer padding, and 12 DIP before the caption buttons.
        const double fixedTitleBarWidth = 104 + 132 + 16 + 12 + 12;
        var tabViewportWidth = Math.Max(0, titleBarWidth - fixedTitleBarWidth);
        var spacingWidth = SpatialShellTabSpacing * tabCount;
        var usableTabWidth = Math.Max(0, tabViewportWidth - SpatialNewTabButtonWidth - spacingWidth);
        var targetWidth = Math.Clamp(
            usableTabWidth / tabCount,
            SpatialMinimumTabWidth,
            SpatialPreferredTabWidth);

        foreach (var child in ShellTabItems.Children)
        {
            if (child is not Border tab) continue;

            if (!tab.Resources.ContainsKey("SpatialTabChromeConfigured"))
            {
                tab.Resources["SpatialTabChromeConfigured"] = true;
                tab.Transitions = [new RepositionThemeTransition()];
            }

            if (Math.Abs(tab.Width - targetWidth) > 0.25)
                tab.Width = targetWidth;

            // PageArc previously pulsed the selected tab opacity. SpatialViewer's
            // approved selected/unselected treatment is static, so stop that legacy
            // composition animation and keep the detached tab fully opaque.
            var compositionVisual = ElementCompositionPreview.GetElementVisual(tab);
            compositionVisual.StopAnimation("Opacity");
            compositionVisual.Opacity = 1f;
        }

        // SpatialViewer keeps available tab labels fully readable in both states;
        // selection is communicated by fill, border, and font weight instead.
        foreach (var visual in _tabItems.Values)
            if (Math.Abs(visual.HeaderText.Opacity - 1) > 0.001)
                visual.HeaderText.Opacity = 1;
    }

    private void ApplySpatialTitleBarColors()
    {
        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        if (AppWindowTitleBar.IsCustomizationSupported())
            AppWindow.TitleBar.PreferredTheme = dark ? TitleBarTheme.Dark : TitleBarTheme.Light;

        var foreground = dark
            ? ColorHelper.FromArgb(255, 240, 245, 245)
            : ColorHelper.FromArgb(255, 21, 32, 32);
        var hoverBackground = dark
            ? ColorHelper.FromArgb(32, 255, 255, 255)
            : ColorHelper.FromArgb(24, 0, 0, 0);
        var pressedBackground = dark
            ? ColorHelper.FromArgb(48, 255, 255, 255)
            : ColorHelper.FromArgb(40, 0, 0, 0);

        AppWindow.TitleBar.ButtonForegroundColor = foreground;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = foreground;
        AppWindow.TitleBar.ButtonHoverForegroundColor = foreground;
        AppWindow.TitleBar.ButtonPressedForegroundColor = foreground;
        AppWindow.TitleBar.ButtonBackgroundColor = ColorHelper.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = ColorHelper.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonHoverBackgroundColor = hoverBackground;
        AppWindow.TitleBar.ButtonPressedBackgroundColor = pressedBackground;
    }
}
