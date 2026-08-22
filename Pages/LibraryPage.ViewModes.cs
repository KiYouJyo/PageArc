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

    private async void BooksGridRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not FrameworkElement element || args.Index < 0 || args.Index >= _visibleBooks.Count) return;
        await LoadPreparedCoverAsync(element, _visibleBooks[args.Index].Id);
    }

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

    private async void BooksListRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is FrameworkElement element)
        {
            ApplyListItemWidth(element);
            var id = args.Index >= 0 && args.Index < _visibleBooks.Count ? _visibleBooks[args.Index].Id : null;
            await LoadPreparedCoverAsync(element, id);
        }
    }

    private void BooksListRepeater_SizeChanged(object sender, SizeChangedEventArgs e) => NormalizeRealizedListWidths();

    private void NormalizeRealizedListWidths()
    {
        if (!string.Equals(_libraryView, "list", StringComparison.OrdinalIgnoreCase)) return;
        var width = GetListViewportWidth();
        if (width <= 0) return;

        // StackLayout measures each item at its desired content width. Give both
        // the host and the repeater an explicit viewport width so short titles
        // cannot shrink the row surface.
        BooksContentHost.Width = width;
        BooksListRepeater.Width = width;
        for (var i = 0; i < _visibleBooks.Count; i++)
        {
            if (BooksListRepeater.TryGetElement(i) is FrameworkElement element)
                element.Width = width;
        }
    }

    private double GetListViewportWidth()
    {
        var width = BooksScrollViewer.ViewportWidth;
        if (width <= 0) width = BooksScrollViewer.ActualWidth;
        if (width <= 0) width = BooksContentHost.ActualWidth;
        if (width <= 0) width = BooksListRepeater.ActualWidth;
        return width;
    }

    private void ApplyListItemWidth(FrameworkElement element)
    {
        var width = GetListViewportWidth();
        if (width > 0) element.Width = width;
    }
}
