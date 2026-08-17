using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class ImportFoldersPage : Page
{
    private readonly ObservableCollection<FolderRow> _rows = [];
    private bool _busy;

    public ImportFoldersPage()
    {
        InitializeComponent();
        FoldersRepeater.ItemsSource = _rows;
        ApplyText();
        RefreshRows();
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var folder = await PickerService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(folder)) return;

        await RunFolderOperationAsync(async () =>
        {
            await App.ImportFolders.AddAsync(folder);
        });
    }

    private async void RescanFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not FrameworkElement { Tag: string id }) return;
        var folder = App.ImportFolders.Folders.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));
        if (folder is null) return;

        await RunFolderOperationAsync(async () =>
        {
            await App.ImportFolders.RescanAsync(folder);
        });
    }

    private void RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not FrameworkElement { Tag: string id }) return;
        var folder = App.ImportFolders.Folders.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));
        if (folder is null) return;
        App.ImportFolders.Remove(folder);
        RefreshRows();
    }

    private async Task RunFolderOperationAsync(Func<Task> operation)
    {
        _busy = true;
        TopAddFolderButton.IsEnabled = false;
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            // User cancellation is not an error.
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Import folder operation failed.", ex);
        }
        finally
        {
            _busy = false;
            TopAddFolderButton.IsEnabled = true;
            RefreshRows();
        }
    }

    private void ApplyText()
    {
        PageTitleText.Text = LocalText("导入文件夹", "フォルダーをインポート", "Import folders");
        PageSubtitleText.Text = LocalText(
            "监视本地文件夹并自动添加受支持的电子书",
            "ローカルフォルダーを監視し、対応する電子書籍を自動的に追加します",
            "Watch local folders and add supported ebooks automatically");
        TopAddFolderText.Text = BottomAddFolderText.Text = LocalText("添加文件夹", "フォルダーを追加", "Add folder");
        InfoBannerText.Text = LocalText(
            "PageArc 会扫描这些文件夹中的 EPUB、MOBI、AZW3、FB2 和 LIT 文件。原始文件不会被移动或修改。",
            "PageArc はこれらのフォルダー内の EPUB、MOBI、AZW3、FB2、LIT ファイルをスキャンします。元のファイルは移動・変更されません。",
            "PageArc scans these folders for EPUB, MOBI, AZW3, FB2 and LIT files. Original files are never moved or modified.");
        AddAnotherTitle.Text = LocalText("添加另一个监视文件夹", "別の監視フォルダーを追加", "Add another watched folder");
        AddAnotherBody.Text = LocalText(
            "适用于在 PageArc 外部管理的书库。",
            "PageArc の外部で管理しているライブラリに適しています。",
            "Useful for libraries managed outside PageArc.");
        EmptyFoldersTitle.Text = LocalText("尚未添加监视文件夹", "監視フォルダーはまだありません", "No watched folders yet");
        EmptyFoldersBody.Text = LocalText("添加文件夹后，PageArc 会扫描其中的电子书。", "フォルダーを追加すると電子書籍をスキャンします。", "Add a folder and PageArc will scan it for ebooks.");
    }

    private void RefreshRows()
    {
        _rows.Clear();
        foreach (var folder in App.ImportFolders.Folders)
        {
            _rows.Add(new FolderRow(
                folder.Id,
                folder.EffectiveName,
                folder.FolderPath,
                FormatBookCount(folder.BookCount),
                FormatScanStatus(folder),
                LocalText("重新扫描", "再スキャン", "Rescan"),
                LocalText("移除", "削除", "Remove")));
        }
        EmptyFoldersCard.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private string FormatBookCount(int count)
    {
        var language = App.Localization.CurrentLanguage;
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return $"{count} 本";
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return $"{count} 冊";
        return count == 1 ? "1 book" : $"{count} books";
    }

    private string FormatScanStatus(ImportFolderEntry folder)
    {
        if (!folder.IsAvailable)
            return LocalText("文件夹不可用", "フォルダーを利用できません", "Folder unavailable");
        if (folder.LastScannedAt is null)
            return LocalText("尚未扫描", "未スキャン", "Not scanned yet");

        var age = DateTimeOffset.Now - folder.LastScannedAt.Value;
        if (age.TotalMinutes < 1) return LocalText("刚刚扫描", "たった今スキャン", "Scanned just now");
        if (age.TotalHours < 1)
        {
            var minutes = Math.Max(1, (int)Math.Round(age.TotalMinutes));
            var language = App.Localization.CurrentLanguage;
            if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return $"{minutes} 分钟前扫描";
            if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return $"{minutes} 分前にスキャン";
            return $"Scanned {minutes} min ago";
        }
        if (age.TotalDays < 1)
        {
            var hours = Math.Max(1, (int)Math.Round(age.TotalHours));
            var language = App.Localization.CurrentLanguage;
            if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return $"{hours} 小时前扫描";
            if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return $"{hours} 時間前にスキャン";
            return $"Scanned {hours} h ago";
        }
        return folder.LastScannedAt.Value.ToLocalTime().ToString("g");
    }

    private static string LocalText(string zh, string ja, string en)
    {
        var language = App.Localization.CurrentLanguage;
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return zh;
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return ja;
        return en;
    }

    private sealed record FolderRow(
        string Id,
        string Name,
        string Path,
        string CountText,
        string ScanText,
        string RescanText,
        string RemoveText);
}
