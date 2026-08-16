using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using PageArc.Pages;
using PageArc.Services;

namespace PageArc;

public sealed partial class MainWindow : Window
{
    private bool _navigating;

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
            StartupDiagnostics.Log("Custom title bar configured.");
            ConfigureTitleBar();
            ApplyTheme();
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

    private void ConfigureTitleBar()
    {
        if (AppWindow?.TitleBar is not { } titleBar) return;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
    }

    private void ApplyTheme()
    {
        RootGrid.RequestedTheme = App.Settings.Current.AppTheme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
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
            App.PendingNavigationTag = tag;

            var target = EnumerateNavigationItems()
                .FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.Ordinal));
            if (target is not null) AppNavigation.SelectedItem = target;

            var navigated = tag switch
            {
                "settings" => ContentFrame.Navigate(typeof(SettingsPage)),
                "about" => ContentFrame.Navigate(typeof(AboutPage)),
                "import-folders" => ContentFrame.Navigate(typeof(ImportFoldersPage)),
                "collections" => ContentFrame.Navigate(typeof(LibraryPage), LibraryMode.Collections),
                "recent" => ContentFrame.Navigate(typeof(LibraryPage), LibraryMode.Recent),
                "favorites" => ContentFrame.Navigate(typeof(LibraryPage), LibraryMode.Favorites),
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

    private IEnumerable<NavigationViewItem> EnumerateNavigationItems() =>
        AppNavigation.MenuItems.OfType<NavigationViewItem>()
            .Concat(AppNavigation.FooterMenuItems.OfType<NavigationViewItem>());

    public void OpenBook(BookEntry book)
    {
        AppNavigation.Visibility = Visibility.Collapsed;
        ReaderFrame.Visibility = Visibility.Visible;
        ReaderFrame.Navigate(typeof(ReaderPage), book);
    }

    public void ExitReader()
    {
        ReaderFrame.Visibility = Visibility.Collapsed;
        AppNavigation.Visibility = Visibility.Visible;
        NavigateTo("library");
    }
}
