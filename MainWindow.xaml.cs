using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PageArc.Models;
using PageArc.Pages;
using PageArc.Services;

namespace PageArc;

public sealed partial class MainWindow : Window
{
    private bool _navigating;
    private bool _isWindowActive = true;
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
                ApplyLocalizedNavigation();
            };
            RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;
            Activated += MainWindow_Activated;
            App.Localization.LanguageChanged += OnLanguageChanged;
            Closed += MainWindow_Closed;

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

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        _isWindowActive = args.WindowActivationState != WindowActivationState.Deactivated;
        ConfigureTitleBar();
        ApplyNavigationPaneBackground();
        StartupDiagnostics.Log($"Window activation changed: active={_isWindowActive}.");
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Activated -= MainWindow_Activated;
        App.Localization.LanguageChanged -= OnLanguageChanged;
        Closed -= MainWindow_Closed;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(ReloadLocalizedShell);
    }

    private void ReloadLocalizedShell()
    {
        var navigationTag = App.PendingNavigationTag;
        var wasPaneOpen = AppNavigation.IsPaneOpen;
        var displayMode = AppNavigation.DisplayMode;

        ApplyLocalizedNavigation();

        if (ReaderFrame.Visibility == Visibility.Visible)
        {
            AppNavigation.IsPaneOpen = wasPaneOpen && displayMode != NavigationViewDisplayMode.Minimal;
            ApplyNavigationPaneBackground();
            return;
        }

        NavigateTo(navigationTag, suppressTransition: true);
        ContentFrame.BackStack.Clear();
        AppNavigation.IsPaneOpen = wasPaneOpen && AppNavigation.DisplayMode != NavigationViewDisplayMode.Minimal;
        ApplyNavigationPaneBackground();
        StartupDiagnostics.Log($"Localized shell reloaded in place: {navigationTag}; window bounds unchanged.");
    }

    private void ApplyLocalizedNavigation()
    {
        ApplyNavigationLabel("library", "Nav_Library.Content");
        ApplyNavigationLabel("categories", "Nav_Categories.Content");
        ApplyNavigationLabel("conversion", "Nav_Conversion.Content");
        ApplyNavigationLabel("import-folders", "Nav_ImportFolders.Content");
        ApplyNavigationLabel("settings", "Nav_Settings.Content");
        ApplyNavigationLabel("about", "Nav_About.Content");
    }

    private void ApplyNavigationLabel(string tag, string resourceKey)
    {
        var item = EnumerateNavigationItems()
            .FirstOrDefault(candidate => string.Equals(candidate.Tag as string, tag, StringComparison.Ordinal));
        if (item is null) return;
        var label = App.Localization.GetString(resourceKey);
        item.Content = label;
        ToolTipService.SetToolTip(item, label);
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
        var isDark = AppNavigation.ActualTheme == ElementTheme.Dark;

        if (!highContrast && _isWindowActive)
        {
            // The cyan pane is an active-window affordance. Pane open/closed and compact/
            // expanded states must not affect the color; only native window activation does.
            var activeColor = isDark
                ? ColorHelper.FromArgb(255, 26, 35, 35)      // #1A2323 deep cyan
                : ColorHelper.FromArgb(255, 229, 249, 249); // #E5F9F9 light cyan
            _navigationSplitView.PaneBackground = new SolidColorBrush(activeColor);
            return;
        }

        var themeKey = highContrast ? "HighContrast" : isDark ? "Dark" : "Light";
        var themeResources = Application.Current.Resources.ThemeDictionaries[themeKey] as ResourceDictionary;
        if (themeResources?["PageArcNavigationPaneRestBrush"] is Brush restingBrush)
            _navigationSplitView.PaneBackground = restingBrush;
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

    public void NavigateTo(string tag) => NavigateTo(tag, suppressTransition: false);

    private void NavigateTo(string tag, bool suppressTransition)
    {
        StartupDiagnostics.Log($"NavigateTo entered: {tag}; suppressTransition={suppressTransition}.");
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

            NavigationTransitionInfo? transition = suppressTransition ? new SuppressNavigationTransitionInfo() : null;
            bool navigated;
            if (suppressTransition)
            {
                navigated = tag switch
                {
                    "settings" => ContentFrame.Navigate(typeof(SettingsPage), null, transition),
                    "about" => ContentFrame.Navigate(typeof(AboutPage), null, transition),
                    "import-folders" => ContentFrame.Navigate(typeof(ImportFoldersPage), null, transition),
                    "categories" => ContentFrame.Navigate(typeof(CategoriesPage), null, transition),
                    "conversion" => ContentFrame.Navigate(typeof(ConversionPage), null, transition),
                    _ => ContentFrame.Navigate(typeof(LibraryPage), LibraryMode.Library, transition)
                };
            }
            else
            {
                navigated = tag switch
                {
                    "settings" => ContentFrame.Navigate(typeof(SettingsPage)),
                    "about" => ContentFrame.Navigate(typeof(AboutPage)),
                    "import-folders" => ContentFrame.Navigate(typeof(ImportFoldersPage)),
                    "categories" => ContentFrame.Navigate(typeof(CategoriesPage)),
                    "conversion" => ContentFrame.Navigate(typeof(ConversionPage)),
                    _ => ContentFrame.Navigate(typeof(LibraryPage), LibraryMode.Library)
                };
            }
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
        StartupDiagnostics.Log($"MainWindow.OpenBook entered: {book.FilePath}.");
        try
        {
            AppNavigation.Visibility = Visibility.Collapsed;
            ReaderFrame.Visibility = Visibility.Visible;
            ReaderFrame.BackStack.Clear();
            var navigated = ReaderFrame.Navigate(typeof(ReaderPage), book, new SuppressNavigationTransitionInfo());
            StartupDiagnostics.Log($"ReaderFrame.Navigate returned {navigated}.");
            if (!navigated)
            {
                ReaderFrame.Visibility = Visibility.Collapsed;
                AppNavigation.Visibility = Visibility.Visible;
            }
            return navigated;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("MainWindow.OpenBook failed", ex);
            ReaderFrame.Visibility = Visibility.Collapsed;
            AppNavigation.Visibility = Visibility.Visible;
            throw;
        }
    }

    public void ExitReader()
    {
        ReaderFrame.Visibility = Visibility.Collapsed;
        AppNavigation.Visibility = Visibility.Visible;
        ReaderFrame.BackStack.Clear();
        NavigateTo("library");
    }
}
