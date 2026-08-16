using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using PageArc.Pages;

namespace PageArc;

public sealed partial class MainWindow : Window
{
    private bool _navigating;

    public MainWindow()
    {
        InitializeComponent();
        Title = "PageArc";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ConfigureTitleBar();
        ApplyTheme();
        NavigateTo(App.PendingNavigationTag);
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
        _navigating = true;
        try
        {
            ReaderFrame.Visibility = Visibility.Collapsed;
            AppNavigation.Visibility = Visibility.Visible;
            App.PendingNavigationTag = tag;

            var target = EnumerateNavigationItems()
                .FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.Ordinal));
            if (target is not null) AppNavigation.SelectedItem = target;

            switch (tag)
            {
                case "settings": ContentFrame.Navigate(typeof(SettingsPage)); break;
                case "about": ContentFrame.Navigate(typeof(AboutPage)); break;
                case "import-folders": ContentFrame.Navigate(typeof(ImportFoldersPage)); break;
                case "collections": ContentFrame.Navigate(typeof(LibraryPage), LibraryMode.Collections); break;
                case "recent": ContentFrame.Navigate(typeof(LibraryPage), LibraryMode.Recent); break;
                case "favorites": ContentFrame.Navigate(typeof(LibraryPage), LibraryMode.Favorites); break;
                default: ContentFrame.Navigate(typeof(LibraryPage), LibraryMode.Library); break;
            }
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
