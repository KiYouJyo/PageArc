using Microsoft.UI;
using Microsoft.UI.Text;
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
    private sealed record ShellTabVisual(Border Container, TextBlock HeaderText);

    private readonly ShellTabSessionManager _tabSessions = new();
    private readonly Dictionary<string, ShellTabVisual> _tabItems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Frame> _readerFrames = new(StringComparer.Ordinal);
    private string? _selectedTabId;
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
                RefreshTabVisuals();
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
        _tabItems[session.Id] = CreateTabVisual(session.Id, HomeTabTitle(), Symbol.Home, 220);
        if (select) SelectTab(session.Id);
        return session;
    }

    private ShellTabVisual CreateTabVisual(string id, string title, Symbol symbol, double width)
    {
        var headerText = new TextBlock
        {
            Text = title,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };

        var content = new Grid { ColumnSpacing = 8 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(new SymbolIcon { Symbol = symbol, FontSize = 14, Opacity = 0.68 });
        Grid.SetColumn(headerText, 1);
        content.Children.Add(headerText);

        var selectButton = new Button
        {
            Tag = id,
            Padding = new Thickness(12, 0, 42, 0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = content
        };
        selectButton.Click += ShellTabSelect_Click;

        var closeButton = new Button
        {
            Tag = id,
            Width = 32,
            Height = 32,
            MinWidth = 32,
            Margin = new Thickness(0, 2, 4, 2),
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new TextBlock { Text = "×", FontSize = 13, Opacity = 0.68 }
        };
        closeButton.Click += ShellTabClose_Click;

        var layer = new Grid();
        layer.Children.Add(selectButton);
        layer.Children.Add(closeButton);

        var container = new Border
        {
            Tag = id,
            Width = width,
            Height = 36,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(0),
            Child = layer
        };
        ShellTabItems.Children.Add(container);
        return new ShellTabVisual(container, headerText);
    }

    private string HomeTabTitle() => RuntimeText.Current("主页", "ホーム", "Home");

    private void UpdateTabHeaders()
    {
        foreach (var session in _tabSessions.Tabs)
        {
            if (!_tabItems.TryGetValue(session.Id, out var item)) continue;
            if (session.Kind == ShellTabKind.Home)
                item.HeaderText.Text = HomeTabTitle();
        }
    }

    private void ShellNewTabButton_Click(object sender, RoutedEventArgs e)
    {
        CreateHomeTab(select: true);
        NavigateTo("library", suppressTransition: true);
        ContentFrame.BackStack.Clear();
    }

    private void ShellTabSelect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id }) SelectTab(id);
    }

    private void ShellTabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id }) CloseTab(id);
    }

    private void SelectTab(string id)
    {
        if (_tabSessions.Find(id) is null) return;
        _selectedTabId = id;
        RefreshTabVisuals();
        ShowSelectedTabSurface();
    }

    private void CloseTab(string id)
    {
        var session = _tabSessions.Find(id);
        if (session is null) return;
        var tabs = _tabSessions.Tabs.ToList();
        var index = tabs.FindIndex(tab => string.Equals(tab.Id, id, StringComparison.Ordinal));
        var wasSelected = string.Equals(_selectedTabId, id, StringComparison.Ordinal);

        if (session.Kind == ShellTabKind.Reader && _readerFrames.Remove(id, out var frame))
        {
            if (frame.Content is ReaderPage reader) reader.PrepareForClose();
            ReaderHost.Children.Remove(frame);
        }

        _tabSessions.Close(id);
        if (_tabItems.Remove(id, out var visual)) ShellTabItems.Children.Remove(visual.Container);

        if (_tabSessions.Tabs.Count == 0)
        {
            CreateHomeTab(select: true);
            NavigateTo("library", suppressTransition: true);
            ContentFrame.BackStack.Clear();
            return;
        }

        if (wasSelected)
        {
            var remaining = _tabSessions.Tabs;
            var next = remaining[Math.Clamp(index, 0, remaining.Count - 1)];
            SelectTab(next.Id);
        }
        else
        {
            RefreshTabVisuals();
            ShowSelectedTabSurface();
        }
    }

    private void RefreshTabVisuals()
    {
        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        foreach (var pair in _tabItems)
        {
            var selected = string.Equals(pair.Key, _selectedTabId, StringComparison.Ordinal);
            var visual = pair.Value;
            visual.Container.Background = new SolidColorBrush(selected
                ? (dark ? ColorHelper.FromArgb(30, 255, 255, 255) : ColorHelper.FromArgb(214, 255, 255, 255))
                : (dark ? ColorHelper.FromArgb(10, 255, 255, 255) : ColorHelper.FromArgb(8, 0, 0, 0)));
            visual.Container.BorderThickness = new Thickness(selected ? 1 : 0);
            visual.Container.BorderBrush = new SolidColorBrush(dark
                ? ColorHelper.FromArgb(38, 255, 255, 255)
                : ColorHelper.FromArgb(51, 117, 117, 117));
            visual.HeaderText.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            visual.HeaderText.Opacity = selected ? 0.9 : 0.68;
        }
    }

    private ShellTabSession EnsureHomeTabSelected()
    {
        var selected = SelectedSession();
        if (selected?.Kind == ShellTabKind.Home) return selected;

        var home = _tabSessions.Tabs.FirstOrDefault(tab => tab.Kind == ShellTabKind.Home)
                   ?? CreateHomeTab(select: false);
        SelectTab(home.Id);
        return home;
    }

    private ShellTabSession? SelectedSession() =>
        string.IsNullOrWhiteSpace(_selectedTabId) ? null : _tabSessions.Find(_selectedTabId);

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
        RefreshTabVisuals();
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
        RefreshTabVisuals();
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
                SelectTab(session.Id);
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

            var title = string.IsNullOrWhiteSpace(book.Title)
                ? Path.GetFileNameWithoutExtension(book.FilePath)
                : book.Title;
            _readerFrames[session.Id] = frame;
            _tabItems[session.Id] = CreateTabVisual(session.Id, title, Symbol.Library, 300);
            ReaderHost.Children.Add(frame);
            SelectTab(session.Id);

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

    public void ExitReader()
    {
        EnsureHomeTabSelected();
        ShowSelectedTabSurface();
    }
}
