using Windows.Storage.Pickers;

namespace PageArc.Services;

public static class PickerService
{
    public static async Task<IReadOnlyList<string>> PickEbooksAsync()
    {
        if (App.MainWindow is null) return [];

        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };

        foreach (var extension in new[] { ".epub", ".fb2", ".mobi", ".azw", ".azw3", ".lit" })
            picker.FileTypeFilter.Add(extension);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var files = await picker.PickMultipleFilesAsync();
        return files.Select(file => file.Path).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
    }

    public static async Task<string?> PickFolderAsync()
    {
        if (App.MainWindow is null) return null;
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        return (await picker.PickSingleFolderAsync())?.Path;
    }
}
