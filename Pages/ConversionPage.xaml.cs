using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using PageArc.Services;
using PageArc.Services.Conversion;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace PageArc.Pages;

public sealed partial class ConversionPage : Page
{
    private readonly EbookConversionService _conversionService = new();
    private readonly ObservableCollection<ConversionQueueItem> _queue = [];
    private bool _running;

    public ConversionPage()
    {
        InitializeComponent();
        QueueList.ItemsSource = _queue;
        UpdateQueueState();
    }

    private async void ChooseFiles_Click(object sender, RoutedEventArgs e)
    {
        var paths = await PickerService.PickEbooksAsync();
        AddPaths(paths);
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (!_running && e.DataView.Contains(StandardDataFormats.StorageItems))
            e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (_running || !e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        AddPaths(items.OfType<StorageFile>().Select(x => x.Path));
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        ConversionInfoBar.IsOpen = false;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
            if (!BookFormatRegistry.IsSupportedPath(path)) continue;
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
        StartConversionButton.IsEnabled = hasItems && !_running;
        OutputFormatCombo.IsEnabled = !_running;
        MetadataCheck.IsEnabled = !_running;
        CoverCheck.IsEnabled = !_running;
        TocCheck.IsEnabled = !_running;
        DropZone.IsHitTestVisible = !_running;
        QueueCountText.Text = string.Format(App.Localization.GetString("Conversion_QueueCount"), _queue.Count);
    }

    private async void StartConversion_Click(object sender, RoutedEventArgs e)
    {
        if (_running || _queue.Count == 0) return;

        var outputFormat = (OutputFormatCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EPUB";
        var options = new EbookConversionOptions(
            MetadataCheck.IsChecked != false,
            CoverCheck.IsChecked != false,
            TocCheck.IsChecked != false);

        _running = true;
        ConversionInfoBar.IsOpen = false;
        UpdateQueueState();

        var succeeded = 0;
        var failed = 0;
        string? firstError = null;
        try
        {
            foreach (var item in _queue)
            {
                item.Status = "…";
                item.OutputPath = null;

                EbookConversionResult result;
                try
                {
                    result = await _conversionService.ConvertAsync(
                        new EbookConversionRequest(item.FilePath, outputFormat, Options: options));
                }
                catch (OperationCanceledException)
                {
                    item.Status = "—";
                    throw;
                }
                catch (Exception ex)
                {
                    result = EbookConversionResult.Failed(ex.Message);
                }

                if (result.Success && !string.IsNullOrWhiteSpace(result.OutputPath))
                {
                    item.OutputPath = result.OutputPath;
                    item.Status = $"✓ {Path.GetFileName(result.OutputPath)}";
                    succeeded++;
                    continue;
                }

                failed++;
                firstError ??= result.ErrorMessage;
                item.Status = result.IsDrmProtected
                    ? "DRM"
                    : $"⚠ {CompactError(result.ErrorMessage)}";
            }

            ConversionInfoBar.Severity = failed == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            ConversionInfoBar.Message = failed == 0
                ? $"{succeeded} / {_queue.Count}"
                : firstError ?? $"{failed} / {_queue.Count}";
            ConversionInfoBar.IsOpen = true;
        }
        finally
        {
            _running = false;
            UpdateQueueState();
        }
    }

    private static string CompactError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "Failed";
        var compact = string.Join(" ", message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 72 ? compact : compact[..69] + "…";
    }
}
