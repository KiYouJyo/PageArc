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
    private readonly ShellTabSessionManager _tabSessions = new();
    private readonly Dictionary<string, TabViewItem> _tabItems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Frame> _readerFrames = new(StringComparer.Ordinal);
    private bool _navigating;
    private bool _tabShellReady;
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
            InitializeTabShell();
            StartupDiagnostics.Log("MainWindow theme and tab shell applied; navigating to initial page.");
            NavigateTo(App.PendingNavigationTag);
            StartupDiagnostics.Log("MainWindow initial navigation completed.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("MainWindow constructor failed", ex);
            throw;
        }
    }

    private void InitializeTabShell()
    {
        if (_tabShellReady) return;
        _tabShellReady = true;
        CreateHomeTab(select: true);
    }

    private ShellTabSession CreateHomeTab(bool select)
    {
        var session = _tabSessions.CreateHome();
        var item = new TabViewItem
        {
            Header = HomeTabTitle(),
            IsClosable = true,
            Tag = session.Id,
            MinWidth = 150,
            MaxWidth = 220
        };
        _tabItems[session.Id] = item;
        ShellTabs.TabItems.Add(item);
        if (select) ShellTabs.SelectedItem = item;
        return session;
    }

    private string HomeTabTitle() => RuntimeText.Current("主页", "ホーム", "Home");

    private void UpdateTabHeaders()
    {
        foreach (var session in _tabSessions.Tabs)
        {
            if (!_tabItems.TryGetValue(session.Id, out var item)) continue;
            if (session.Kind == ShellTabKind.Home)
                item.Header = HomeTabTitle();
        }
    }

    private ShellTabSession EnsureHomeTabSelected()
    {
        var selected = SelectedSession();
        if (selected?.Kind == ShellTabKind.Home) return selected;

        var home = _tabSessions.Tabs.FirstOrDefault(tab => tab.Kind == ShellTabKind.Home)
                   ?? CreateHomeTab(select: false);
        if (_tabItems.TryGetValue(home.Id, out var item)) ShellTabs.SelectedItem = item;
        ShowSelectedTabSurface();
        return home;
    }

    private ShellTabSession? SelectedSession()
    {
        if (ShellTabs.SelectedItem is not TabViewItem { Tag: string id }) return null;
        return _tabSessions.Find(id);
    }

    private void ShellTabs_AddTabButtonClick(TabView sender, object args) => CreateHomeTab(select: true);

    private void ShellTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_tabShellReady) return;
        ShowSelectedTabSurface();
    }

    private void ShellTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab is not TabViewItem { Tag: string id } item) return;
        var session = _tabSessions.Find(id);
        if (session is null) return;

        var index = ShellTabs.TabItems.IndexOf(item);
        if (session.Kind == ShellTabKind.Reader && _readerFrames.Remove(id, out var frame))
        {
            if (frame.Content is ReaderPage reader) reader.PrepareForClose();
            ReaderHost.Children.Remove(frame);
        }

        _tabSessions.Close(id);
        _tabItems.Remove(id);
        ShellTabs.TabItems.Remove(item);

        if (ShellTabs.TabItems.Count == 0)
        {
            CreateHomeTab(select: true);
            return;
        }

        if (ShellTabs.SelectedItem is null)
            ShellTabs.SelectedItem = ShellTabs.TabItems[Math.Clamp(index, 0, ShellTabs.TabItems.Count - 1)];
        ShowSelectedTabSurface();
    }

    private void ShowSelectedTabSurface()
    {
        var session = SelectedSession();
        var readerSelected = session?.Kind == ShellTabKind.Reader;
        AppNavigation.Visibility = readerSelected ? Visibility.Collapsed : Visibility.Visible;
        ReaderHost.Visibility = readerSelected ? Visibility.Visible : Visibility.Collapsed;

        foreach (var pair in _readerFrames)
            pair.Value.Visibility = readerSelected && string.Equals(pair.Key, session?.Id, StringComparison.Ordinal)
                ? Visibility.Visible
                : Visibility.Collapsed;
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
        foreach (var frame in _readerFrames.Values)
        {
            if (frame.Content is ReaderPage reader) reader.PrepareForClose();
        }
        _readerFrames.Clear();
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
        UpdateTabHeaders();

        if (SelectedSession()?.Kind == ShellTabKind.Reader)
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

    private void QueueNavigationPaneBackgroundUpdate() => DispatcherQueue.TryEnqueue(ApplyNavigationPaneBackground);

    private void ApplyNavigationPaneBackground()
    {
        _navigationSplitView ??= FindDescendant<SplitView>(AppNavigation);
        if (_navigationSplitView is null) return;

        var highContrast = new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
        var isDark = AppNavigation.ActualTheme == ElementTheme.Dark;

        if (!highContrast && _isWindowActive)
        {
            var activeColor = isDark
                ? ColorHelper.FromArgb(255, 26, 35, 35)
                : ColorHelper.FromArgb(255, 229, 249, 249);
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
            EnsureHomeTabSelected();
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
            EnsureHomeTabSelected();
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
            var (session, created) = _tabSessions.OpenReader(book.Id);
            if (!created)
            {
                if (_tabItems.TryGetValue(session.Id, out var existingItem)) ShellTabs.SelectedItem = existingItem;
                ShowSelectedTabSurface();
                return true;
            }

            var frame = new Frame { Visibility = Visibility.Collapsed };
            var navigated = frame.Navigate(typeof(ReaderPage), book, new SuppressNavigationTransitionInfo());
            StartupDiagnostics.Log($"Reader tab Frame.Navigate returned {navigated}.");
            if (!navigated)
            {
                _tabSessions.Close(session.Id);
                return false;
            }

            var item = new TabViewItem
            {
                Header = string.IsNullOrWhiteSpace(book.Title) ? Path.GetFileNameWithoutExtension(book.FilePath) : book.Title,
                IsClosable = true,
                Tag = session.Id,
                MinWidth = 190,
                MaxWidth = 260
            };
            _readerFrames[session.Id] = frame;
            _tabItems[session.Id] = item;
            ReaderHost.Children.Add(frame);
            ShellTabs.TabItems.Add(item);
            ShellTabs.SelectedItem = item;
            ShowSelectedTabSurface();

            App.Library.MarkOpened(book);
            _ = App.JumpLists.RecordRecentBookAsync(book);
            return true;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("MainWindow.OpenBook failed", ex);
            throw;
        }
    }

    // Retained as a compatibility route for older callers. The reader toolbar no longer exposes
    // a back-to-library action; selecting a Home tab is now the navigation model.
    public void ExitReader()
    {
        EnsureHomeTabSelected();
        ShowSelectedTabSurface();
    }
}
