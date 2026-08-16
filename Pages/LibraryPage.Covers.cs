using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using PageArc.Models;
using PageArc.Services;
using Windows.Storage;

namespace PageArc.Pages;

public sealed partial class LibraryPage
{
    private async void CoverImage_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image { Tag: string id } image) return;
        var book = App.Library.FindById(id);
        if (book is null || string.IsNullOrWhiteSpace(book.CoverPath) || !File.Exists(book.CoverPath))
        {
            image.Source = null;
            image.Opacity = 0;
            return;
        }

        try
        {
            var bitmap = await LoadCoverBitmapAsync(book.CoverPath, 520);
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

    private async Task LoadDetailsCoverAsync(BookEntry book)
    {
        DetailsCoverImage.Source = null;
        DetailsCoverImage.Opacity = 0;
        if (string.IsNullOrWhiteSpace(book.CoverPath) || !File.Exists(book.CoverPath)) return;
        var expectedId = book.Id;
        try
        {
            var bitmap = await LoadCoverBitmapAsync(book.CoverPath, 420);
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
