using System.Globalization;
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
    private readonly ConversionRuntimeManager _runtimeManager = ConversionRuntimeManager.Shared;
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

        try
        {
            if (!await EnsureConversionRuntimeReadyAsync())
                return;

            var succeeded = 0;
            var failed = 0;
            string? firstError = null;

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
            RuntimeDownloadPanel.Visibility = Visibility.Collapsed;
            _running = false;
            UpdateQueueState();
        }
    }

    private async Task<bool> EnsureConversionRuntimeReadyAsync()
    {
        if (new CalibreConversionProvider().IsAvailable || _runtimeManager.IsInstalled)
            return true;

        var sizeMb = ConversionRuntimeManager.ExpectedArchiveSize / (1024d * 1024d);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = LocalText("需要转换内核", "変換ランタイムが必要です", "Conversion runtime required"),
            Content = LocalText(
                $"此操作需要 PageArc Conversion Runtime（calibre {ConversionRuntimeManager.CalibreVersion}）。基础阅读器不包含该组件；首次使用需从 PageArc.ConversionRuntime 下载约 {sizeMb:0} MB，完成 SHA-256 校验后安装到本机用户目录。",
                $"この操作には PageArc Conversion Runtime（calibre {ConversionRuntimeManager.CalibreVersion}）が必要です。基本リーダーには含まれていません。初回のみ PageArc.ConversionRuntime から約 {sizeMb:0} MB をダウンロードし、SHA-256 検証後にユーザー領域へインストールします。",
                $"This action needs PageArc Conversion Runtime (calibre {ConversionRuntimeManager.CalibreVersion}). It is not included in the base reader. The first use downloads about {sizeMb:0} MB from PageArc.ConversionRuntime and installs it per-user after SHA-256 verification."),
            PrimaryButtonText = LocalText("下载并继续", "ダウンロードして続行", "Download and continue"),
            CloseButtonText = LocalText("取消", "キャンセル", "Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return false;

        RuntimeDownloadPanel.Visibility = Visibility.Visible;
        RuntimeDownloadProgressBar.Value = 0;
        RuntimeDownloadText.Text = LocalText("正在检查转换内核…", "変換ランタイムを確認しています…", "Checking conversion runtime…");

        try
        {
            var progress = new Progress<ConversionRuntimeProgress>(value =>
            {
                var percent = value.TotalBytes is > 0 ? value.Fraction * 100 : 0;
                RuntimeDownloadProgressBar.Value = percent;
                RuntimeDownloadText.Text = value.Stage switch
                {
                    "manifest" => LocalText("正在检查转换内核清单…", "変換ランタイムのマニフェストを確認しています…", "Checking conversion runtime manifest…"),
                    "download" => LocalText(
                        $"正在下载转换内核… {percent:0}%  {FormatBytes(value.BytesTransferred)} / {FormatBytes(value.TotalBytes ?? 0)}",
                        $"変換ランタイムをダウンロードしています… {percent:0}%  {FormatBytes(value.BytesTransferred)} / {FormatBytes(value.TotalBytes ?? 0)}",
                        $"Downloading conversion runtime… {percent:0}%  {FormatBytes(value.BytesTransferred)} / {FormatBytes(value.TotalBytes ?? 0)}"),
                    "extract" => LocalText("下载完成，正在校验并安装…", "ダウンロード完了。検証してインストールしています…", "Download complete; verifying and installing…"),
                    "complete" => LocalText("转换内核安装完成。", "変換ランタイムのインストールが完了しました。", "Conversion runtime installed."),
                    _ => LocalText("正在准备转换内核…", "変換ランタイムを準備しています…", "Preparing conversion runtime…")
                };
            });

            await _runtimeManager.EnsureInstalledAsync(progress);
            ConversionInfoBar.Severity = InfoBarSeverity.Success;
            ConversionInfoBar.Message = LocalText(
                $"转换内核 {ConversionRuntimeManager.PackageVersion} 已安装，之后无需重复下载。",
                $"変換ランタイム {ConversionRuntimeManager.PackageVersion} をインストールしました。次回から再ダウンロードは不要です。",
                $"Conversion runtime {ConversionRuntimeManager.PackageVersion} installed. It will not be downloaded again unless removed.");
            ConversionInfoBar.IsOpen = true;
            return true;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("On-demand conversion runtime installation failed", ex);
            ConversionInfoBar.Severity = InfoBarSeverity.Error;
            ConversionInfoBar.Message = LocalText(
                "转换内核下载或校验失败，请检查网络后重试。",
                "変換ランタイムのダウンロードまたは検証に失敗しました。ネットワークを確認して再試行してください。",
                "The conversion runtime could not be downloaded or verified. Check the network and try again.");
            ConversionInfoBar.IsOpen = true;
            return false;
        }
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / (1024d * 1024d):0.0} MB"
            : $"{bytes / 1024d:0.0} KB";

    private static string LocalText(string zh, string ja, string en)
    {
        var language = CultureInfo.CurrentUICulture.Name;
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return zh;
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return ja;
        return en;
    }

    private static string CompactError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "Failed";
        var compact = string.Join(" ", message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 72 ? compact : compact[..69] + "…";
    }
}
