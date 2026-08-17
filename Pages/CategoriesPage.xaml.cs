using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;

namespace PageArc.Pages;

public sealed partial class CategoriesPage : Page
{
    private readonly ObservableCollection<CategoryEntry> _visibleCategories = [];

    public CategoriesPage()
    {
        InitializeComponent();
        CategoriesRepeater.ItemsSource = _visibleCategories;
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        var query = SearchBox.Text?.Trim();
        IEnumerable<CategoryEntry> categories = App.Categories.Categories;
        if (!string.IsNullOrWhiteSpace(query))
            categories = categories.Where(x => x.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase));

        _visibleCategories.Clear();
        foreach (var category in categories.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            category.BookCount = App.Library.Books.Count(book =>
                string.Equals(book.Collection, category.Name, StringComparison.CurrentCultureIgnoreCase));
            _visibleCategories.Add(category);
        }

        var empty = _visibleCategories.Count == 0;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        CategoriesScrollViewer.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) Refresh();
    }

    private async void NewCategory_Click(object sender, RoutedEventArgs e)
    {
        var input = new TextBox
        {
            PlaceholderText = App.Localization.GetString("Categories_NamePlaceholder")
        };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = App.Localization.GetString("Categories_NewDialogTitle"),
            Content = input,
            PrimaryButtonText = App.Localization.GetString("Categories_Create"),
            CloseButtonText = App.Localization.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(input.Text)) return;
        App.Categories.Add(input.Text);
        Refresh();
    }

    private void CategoryCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        var name = element.Tag as string
            ?? (element.DataContext as CategoryEntry)?.Name;
        if (string.IsNullOrWhiteSpace(name)) return;
        App.MainWindow?.OpenCategory(name);
    }
}
