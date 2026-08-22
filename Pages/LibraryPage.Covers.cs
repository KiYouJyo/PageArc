using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PageArc.Models;
using PageArc.Services;
using Windows.Storage;

namespace PageArc.Pages;

public sealed partial class LibraryPage
{
    private readonly Dictionary<string, BitmapImage> _coverBitmapCache = new(StringComparer.OrdinalIgnoreCase);

    private async void CoverImage_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image image) return;
        if (image.Tag is not string)
        {
            DispatcherQueue.TryEnqueue(async () => await LoadCoverImageAsync(image));
            return;
        }
        await LoadCoverImageAsync(image);
    }

    private async Task LoadCoverImageAsync(Image image)
    {
        if (image.Tag is not string id) return;
        var book = App.Library.FindById(id);
        if (book is null) return;

        if (string.IsNullOrWhiteSpace(book.CoverPath) || !File.Exists(book.CoverPath))
        {
            image.Source = null;
            image.Opacity = 0;
            return;
        }

        try
        {
            if (!_coverBitmapCache.TryGetValue(book.CoverPath, out var bitmap))
            {
                bitmap = await LoadCoverBitmapAsync(book.CoverPath, 520);
                if (bitmap is not null) _coverBitmapCache[book.CoverPath] = bitmap;
            }
            if (!string.Equals(image.Tag as string, id, StringComparison.Ordinal)) return;
            image.Source = bitmap;
            image.Opacity = bitmap is null ? 0 : 1;
        }
        catch (Exception ex)
        {
            image.Source = null;
            image.Opacity = 0;
            StartupDiagnostics.Log($"Failed to load cached cover for '{book.Title}'.", ex);
        }
    }

    private async Task LoadPreparedCoverAsync(DependencyObject root, string? preparedId = null)
    {
        var image = FindCoverImage(root);
        if (image is null) return;
        if (!string.IsNullOrWhiteSpace(preparedId)) image.Tag = preparedId;
        await LoadCoverImageAsync(image);
    }

    private static Image? FindCoverImage(DependencyObject root)
    {
        if (root is Image image) return image;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var found = FindCoverImage(VisualTreeHelper.GetChild(root, index));
            if (found is not null) return found;
        }
        return null;
    }

    private async Task LoadDetailsCoverAsync(BookEntry book)
    {
        DetailsCoverImage.Source = null;
        DetailsCoverImage.Opacity = 0;
        if (string.IsNullOrWhiteSpace(book.CoverPath) || !File.Exists(book.CoverPath)) return;
        var expectedId = book.Id;
        try
        {
            if (!_coverBitmapCache.TryGetValue(book.CoverPath, out var bitmap))
            {
                bitmap = await LoadCoverBitmapAsync(book.CoverPath, 420);
                if (bitmap is not null) _coverBitmapCache[book.CoverPath] = bitmap;
            }
            if (_detailsBook is null || !string.Equals(_detailsBook.Id, expectedId, StringComparison.Ordinal)) return;
            DetailsCoverImage.Source = bitmap;
            DetailsCoverImage.Opacity = bitmap is null ? 0 : 1;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"Failed to load details cover for '{book.Title}'.", ex);
        }
    }

    private static async Task<BitmapImage?> LoadCoverBitmapAsync(string path, int decodePixelWidth)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var bitmap = new BitmapImage { DecodePixelWidth = decodePixelWidth };
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }
}
