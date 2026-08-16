using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace PageArc.Pages;

public sealed partial class ConversionPage : Page
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".epub", ".fb2", ".mobi", ".azw", ".azw3", ".lit"
    };

    private readonly ObservableCollection<ConversionQueueItem> _queue = [];

    public ConversionPage()
    {
        InitializeComponent();
        QueueList.ItemsSource = _queue;
        UpdateQueueState();
    }

    private async void ChooseFiles_Click(object sender, RoutedEventArgs e)
    {
        var paths = await Services.PickerService.PickEbooksAsync();
        AddPaths(paths);
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
            e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        AddPaths(items.OfType<StorageFile>().Select(x => x.Path));
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        ConversionInfoBar.IsOpen = false;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
            if (!SupportedExtensions.Contains(Path.GetExtension(path))) continue;
            if (_queue.Any(x => string.Equals(x.FilePath, path, StringComparison.OrdinalIgnoreCase))) continue;
            _queue.Add(new ConversionQueueItem
            {
                FilePath = path,
                Status = App.Localization.GetString("Conversion_Ready")
            });
        }
        UpdateQueueState();
    }

    private void UpdateQueueState()
    {
        var hasItems = _queue.Count > 0;
        EmptyQueue.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        QueueList.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        StartConversionButton.IsEnabled = hasItems;
        QueueCountText.Text = string.Format(App.Localization.GetString("Conversion_QueueCount"), _queue.Count);
    }

    private void StartConversion_Click(object sender, RoutedEventArgs e)
    {
        ConversionInfoBar.Severity = InfoBarSeverity.Informational;
        ConversionInfoBar.Message = App.Localization.GetString("Conversion_EnginePending");
        ConversionInfoBar.IsOpen = true;
    }
}
