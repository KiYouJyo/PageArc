using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PageArc.Models;
using PageArc.Pages;
using PageArc.Services;
using Windows.Foundation;

namespace PageArc;

public sealed partial class MainWindow : Window
{
    private bool _navigating;
    private bool _themeReady;
    private ElementTheme _lastActualTheme = ElementTheme.Default;

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
                _lastActualTheme = RootGrid.ActualTheme;
                _themeReady = true;
                ConfigureTitleBar();
            };
            RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;
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
        var current = RootGrid.ActualTheme;
        if (_themeReady && _lastActualTheme != ElementTheme.Default && _lastActualTheme != current)
            BeginThemeTransition(_lastActualTheme);
        _lastActualTheme = current;
        ConfigureTitleBar();
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
    }

    private void BeginThemeTransition(ElementTheme previousTheme)
    {
        ThemeTransitionOverlay.Background = CreateTransitionBrush(previousTheme);
        ThemeTransitionOverlay.Opacity = 1;
        ThemeTransitionOverlay.Visibility = Visibility.Visible;

        var animation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(280),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, ThemeTransitionOverlay);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) =>
        {
            ThemeTransitionOverlay.Opacity = 0;
            ThemeTransitionOverlay.Visibility = Visibility.Collapsed;
        };
        storyboard.Begin();
    }

    private static LinearGradientBrush CreateTransitionBrush(ElementTheme theme)
    {
        var dark = theme == ElementTheme.Dark;
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        if (dark)
        {
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 16, 42, 46), Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 11, 32, 36), Offset = 0.5 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 7, 23, 26), Offset = 1 });
        }
        else
        {
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 248, 252, 251), Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 238, 248, 247), Offset = 0.48 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 229, 242, 243), Offset = 1 });
        }
        return brush;
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
