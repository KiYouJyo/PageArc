using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PageArc.Models;
using PageArc.Pages;
using PageArc.Services;

namespace PageArc;

public sealed partial class MainWindow : Window
{
    private bool _navigating;
    private SplitView? _navigationSplitView;

    public MainWindow()
    {
        StartupDiagnostics.Log("MainWindow constructor entered.");
        try
        {
            InitializeComponent();
            StartupDiagnostics.Log("MainWindow.InitializeComponent completed.");
            Title = "PageArc";
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            RootGrid.Loaded += (_, _) =>
            {
                ConfigureTitleBar();
                ApplyNavigationPaneBackground();
            };
            RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;
            AppNavigation.PaneOpening += (_, _) => ApplyNavigationPaneBackground();
            AppNavigation.PaneOpened += (_, _) => ApplyNavigationPaneBackground();

            StartupDiagnostics.Log("Custom title bar configured.");
            ApplyAppTheme(App.Settings.Current.AppTheme);
            StartupDiagnostics.Log("MainWindow theme applied; navigating to initial page.");
            NavigateTo(App.PendingNavigationTag);
            StartupDiagnostics.Log("MainWindow initial navigation completed.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("MainWindow constructor failed", ex);
            throw;
        }
    }

    private void RootGrid_ActualThemeChanged(FrameworkElement sender, object args)
    {
        ConfigureTitleBar();
        QueueNavigationPaneBackgroundUpdate();
    }

    private void ConfigureTitleBar()
    {
        if (AppWindow?.TitleBar is not { } titleBar) return;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = RootGrid.ActualTheme == ElementTheme.Dark ? Colors.White : Colors.Black;
        titleBar.ButtonInactiveForegroundColor = RootGrid.ActualTheme == ElementTheme.Dark ? Colors.LightGray : Colors.DimGray;
    }

    public void ApplyAppTheme(string theme)
    {
        RootGrid.RequestedTheme = theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        ConfigureTitleBar();
        QueueNavigationPaneBackgroundUpdate();
    }

    private void AppNavigation_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        // Match UrbanPlanToolbox/standard NavigationView adaptive behavior:
        // narrow windows enter Minimal mode, leaving only the hamburger button.
        // Opening the pane then overlays the content instead of reserving a permanent rail.
        if (args.DisplayMode == NavigationViewDisplayMode.Minimal)
            sender.IsPaneOpen = false;
        ApplyNavigationPaneBackground();
    }

    private void QueueNavigationPaneBackgroundUpdate() =>
        DispatcherQueue.TryEnqueue(ApplyNavigationPaneBackground);

    private void ApplyNavigationPaneBackground()
    {
        _navigationSplitView ??= FindDescendant<SplitView>(AppNavigation);
        if (_navigationSplitView is null) return;

        var highContrast = new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
        var themeKey = highContrast
            ? "HighContrast"
            : AppNavigation.ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
        var themeResources = Application.Current.Resources.ThemeDictionaries[themeKey] as ResourceDictionary;
        if (themeResources?["PageArcNavigationPaneBrush"] is Brush brush)
            _navigationSplitView.PaneBackground = brush;
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            var descendant = FindDescendant<T>(child);
            if (descendant is not null) return descendant;
        }
        return null;
    }

    private void AppNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_navigating || args.SelectedItemContainer?.Tag is not string tag) return;
        NavigateTo(tag);
    }

    public void NavigateTo(string tag)
    {
        StartupDiagnostics.Log($"NavigateTo entered: {tag}");
        _navigating = true;
        try
        {
            ReaderFrame.Visibility = Visibility.Collapsed;
            AppNavigation.Visibility = Visibility.Visible;
            tag = tag switch
            {
                "recent" or "favorites" or "collections" => "library",
                _ => tag
            };
            App.PendingNavigationTag = tag;

            var target = EnumerateNavigationItems()
                .FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.Ordinal));
            if (target is not null) AppNavigation.SelectedItem = target;

            var navigated = tag switch
            {
                "settings" => ContentFrame.Navigate(typeof(SettingsPage)),
                "about" => ContentFrame.Navigate(typeof(AboutPage)),
                "import-folders" => ContentFrame.Navigate(typeof(ImportFoldersPage)),
                "categories" => ContentFrame.Navigate(typeof(CategoriesPage)),
                "conversion" => ContentFrame.Navigate(typeof(ConversionPage)),
                _ => ContentFrame.Navigate(typeof(LibraryPage), LibraryMode.Library)
            };
            StartupDiagnostics.Log($"Frame.Navigate returned {navigated} for {tag}.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"NavigateTo failed: {tag}", ex);
            throw;
        }
        finally
        {
            _navigating = false;
        }
    }

    public void OpenCategory(string categoryName)
    {
        _navigating = true;
        try
        {
            ReaderFrame.Visibility = Visibility.Collapsed;
            AppNavigation.Visibility = Visibility.Visible;
            var target = EnumerateNavigationItems()
                .FirstOrDefault(item => string.Equals(item.Tag as string, "categories", StringComparison.Ordinal));
            if (target is not null) AppNavigation.SelectedItem = target;
            ContentFrame.Navigate(typeof(LibraryPage), $"category:{categoryName}");
        }
        finally
        {
            _navigating = false;
        }
    }

    private IEnumerable<NavigationViewItem> EnumerateNavigationItems() =>
        AppNavigation.MenuItems.OfType<NavigationViewItem>()
            .Concat(AppNavigation.FooterMenuItems.OfType<NavigationViewItem>());

    public bool OpenBook(BookEntry book)
    {
        AppNavigation.Visibility = Visibility.Collapsed;
        ReaderFrame.Visibility = Visibility.Visible;
        var navigated = ReaderFrame.Navigate(typeof(ReaderPage), book);
        if (!navigated)
        {
            ReaderFrame.Visibility = Visibility.Collapsed;
            AppNavigation.Visibility = Visibility.Visible;
        }
        return navigated;
    }

    public void ExitReader()
    {
        ReaderFrame.Visibility = Visibility.Collapsed;
        AppNavigation.Visibility = Visibility.Visible;
        NavigateTo("library");
    }
}
