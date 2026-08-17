using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PageArc.Pages;

public sealed partial class LibraryPage
{
    // Keep the existing constructor assignment compatible while the Figma-converged
    // grid repeater has a more explicit generated name.
    private ItemsRepeater BooksRepeater => BooksGridRepeater;
    private string _libraryView = "grid";

    private void ViewModeButton_Click(object sender, RoutedEventArgs e)
    {
        _libraryView = string.Equals(_libraryView, "list", StringComparison.OrdinalIgnoreCase)
            ? "grid"
            : "list";
        ApplyLibraryViewMode();
        PersistLibraryViewPreference();
    }

    private void ApplyLibraryViewMode()
    {
        var isList = string.Equals(_libraryView, "list", StringComparison.OrdinalIgnoreCase);
        BooksGridRepeater.Visibility = isList ? Visibility.Collapsed : Visibility.Visible;
        BooksListRepeater.Visibility = isList ? Visibility.Visible : Visibility.Collapsed;
        BooksListRepeater.ItemsSource ??= _visibleBooks;

        ViewModeIcon.Symbol = isList ? Symbol.Bullets : Symbol.ViewAll;
        ViewModeText.Text = isList
            ? LocalText("列表", "リスト", "List")
            : LocalText("网格", "グリッド", "Grid");
    }
}
