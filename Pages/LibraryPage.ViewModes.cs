using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PageArc.Pages;

public sealed partial class LibraryPage
{
    // Keep the existing constructor assignment compatible while the Figma-converged
    // grid repeater has a more explicit generated name.
    private ItemsRepeater BooksRepeater => BooksGridRepeater;
    private string _libraryView = "grid";
    private bool _listWidthHooked;

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

        if (isList)
        {
            EnsureListWidthNormalization();
            NormalizeRealizedListWidths();
        }

        ViewModeIcon.Symbol = isList ? Symbol.Bullets : Symbol.ViewAll;
        ViewModeText.Text = isList
            ? LocalText("列表", "リスト", "List")
            : LocalText("网格", "グリッド", "Grid");
    }

    private void EnsureListWidthNormalization()
    {
        if (_listWidthHooked) return;
        _listWidthHooked = true;
        BooksListRepeater.ElementPrepared += BooksListRepeater_ElementPrepared;
        BooksListRepeater.SizeChanged += BooksListRepeater_SizeChanged;
        BooksScrollViewer.SizeChanged += BooksListRepeater_SizeChanged;
    }

    private void BooksListRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is FrameworkElement element)
            ApplyListItemWidth(element);
    }

    private void BooksListRepeater_SizeChanged(object sender, SizeChangedEventArgs e) => NormalizeRealizedListWidths();

    private void NormalizeRealizedListWidths()
    {
        if (!string.Equals(_libraryView, "list", StringComparison.OrdinalIgnoreCase)) return;
        for (var i = 0; i < _visibleBooks.Count; i++)
        {
            if (BooksListRepeater.TryGetElement(i) is FrameworkElement element)
                ApplyListItemWidth(element);
        }
    }

    private void ApplyListItemWidth(FrameworkElement element)
    {
        var width = BooksScrollViewer.ViewportWidth;
        if (width <= 0) width = BooksListRepeater.ActualWidth;
        if (width > 0) element.Width = width;
    }
}
