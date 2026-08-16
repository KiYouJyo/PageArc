using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PageArc.Models;
using PageArc.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace PageArc.Pages;

public sealed partial class LibraryPage
{
    private CancellationTokenSource? _importCts;
    private bool _importRunning;

    private Task ShowImportDialogAsync(bool browseFolderImmediately = false)
    {
        ImportInfoBar.IsOpen = false;
        ImportOverlay.Visibility = Visibility.Visible;
        BuildImportChooser();
        if (browseFolderImmediately) _ = BrowseImportFolderAsync();
        return Task.CompletedTask;
    }

    private void BuildImportChooser()
    {
        _importRunning = false;
        ImportDialogContentHost.Children.Clear();
        var root = new Grid { Width = 620, Height = 440 };
        ImportDialogContentHost.Children.Add(root);

        root.Children.Add(TextAt(LocalText("导入书籍", "書籍をインポート", "Import books"), 23, 21, 560, 28, 20, true));
        var close = IconButton("\uE8BB", 32, 32);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Margin = new Thickness(0, 12, 16, 0);
        close.Click += (_, _) => HideImportOverlay();
        root.Children.Add(close);

        root.Children.Add(TextAt(
            LocalText("将电子书文件添加到本地 PageArc 书库。", "電子書籍ファイルをローカルの PageArc ライブラリに追加します。", "Add ebook files to your local PageArc library."),
            23, 57, 560, 22, 14, false, 0.61));

        var dropZone = new Grid
        {
            Width = 572,
            Height = 190,
            Margin = new Thickness(23, 95, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            AllowDrop = true,
            Background = Brush(10, 0, 95, 184)
        };
        var outline = new Rectangle
        {
            Stroke = Brush(90, 0, 95, 184),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 5, 4 },
            RadiusX = 8,
            RadiusY = 8
        };
        dropZone.Children.Add(outline);
        var dropContent = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 7 };
        dropContent.Children.Add(new FontIcon { Glyph = "\uE896", FontSize = 34, Opacity = 0.55, HorizontalAlignment = HorizontalAlignment.Center });
        dropContent.Children.Add(new TextBlock
        {
            Text = LocalText("将电子书文件拖放到这里", "電子書籍ファイルをここにドロップ", "Drop ebook files here"),
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        dropContent.Children.Add(new TextBlock
        {
            Text = "EPUB · MOBI · AZW3 · FB2 · LIT",
            FontSize = 13,
            Opacity = 0.52,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        dropZone.Children.Add(dropContent);
        dropZone.DragOver += ImportDropZone_DragOver;
        dropZone.Drop += ImportDropZone_Drop;
        root.Children.Add(dropZone);

        var browseFiles = ActionButton(LocalText("浏览文件", "ファイルを選択", "Browse files"), 132, true);
        browseFiles.Margin = new Thickness(165, 305, 0, 0);
        browseFiles.HorizontalAlignment = HorizontalAlignment.Left;
        browseFiles.VerticalAlignment = VerticalAlignment.Top;
        browseFiles.Click += async (_, _) => await BrowseImportFilesAsync();
        root.Children.Add(browseFiles);

        var browseFolder = ActionButton(LocalText("浏览文件夹", "フォルダーを選択", "Browse folder"), 132, false);
        browseFolder.Margin = new Thickness(309, 305, 0, 0);
        browseFolder.HorizontalAlignment = HorizontalAlignment.Left;
        browseFolder.VerticalAlignment = VerticalAlignment.Top;
        browseFolder.Click += async (_, _) => await BrowseImportFolderAsync();
        root.Children.Add(browseFolder);

        root.Children.Add(TextAt(
            LocalText("暂不支持受 DRM 保护的电子书。", "DRM 保護された電子書籍には対応していません。", "DRM-protected ebooks are not supported."),
            23, 373, 330, 18, 12, false, 0.5));

        var cancel = ActionButton(LocalText("取消", "キャンセル", "Cancel"), 92, false);
        cancel.Margin = new Thickness(503, 385, 0, 0);
        cancel.HorizontalAlignment = HorizontalAlignment.Left;
        cancel.VerticalAlignment = VerticalAlignment.Top;
        cancel.Click += (_, _) => HideImportOverlay();
        root.Children.Add(cancel);
    }

    private async Task BrowseImportFilesAsync()
    {
        var paths = await PickerService.PickEbooksAsync();
        if (paths.Count > 0) await RunLibraryImportAsync(paths);
    }

    private async Task BrowseImportFolderAsync()
    {
        var folder = await PickerService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(folder)) return;
        var paths = await Task.Run(() => ImportFolderService.EnumerateSupportedFiles(folder));
        await RunLibraryImportAsync(paths);
    }

    private void ImportDropZone_DragOver(object sender, DragEventArgs e)
    {
        if (_importRunning || !e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void ImportDropZone_Drop(object sender, DragEventArgs e)
    {
        if (_importRunning || !e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var items = await e.DataView.GetStorageItemsAsync();
        var paths = new List<string>();
        foreach (var item in items)
        {
            if (item is StorageFile file)
            {
                paths.Add(file.Path);
                continue;
            }
            if (item is StorageFolder folder)
                paths.AddRange(await Task.Run(() => ImportFolderService.EnumerateSupportedFiles(folder.Path)));
        }
        if (paths.Count > 0) await RunLibraryImportAsync(paths);
    }

    private async Task RunLibraryImportAsync(IEnumerable<string> inputPaths)
    {
        var paths = inputPaths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0)
        {
            ShowImportCompletion(new LibraryImportSummary([]));
            return;
        }

        _importCts?.Cancel();
        _importCts?.Dispose();
        _importCts = new CancellationTokenSource();
        _importRunning = true;
        var rows = paths.Select(path => new ImportVisualRow(path, System.IO.Path.GetFileName(path))).ToArray();
        BuildImportProgress(rows, 0, paths.Length);
        var results = new List<LibraryImportItemResult>(paths.Length);

        try
        {
            for (var index = 0; index < paths.Length; index++)
            {
                _importCts.Token.ThrowIfCancellationRequested();
                UpdateProgressRow(rows[index], ImportRowState.Working);
                UpdateImportProgressHeader(index, paths.Length);
                var result = await App.Library.ImportDetailedAsync(paths[index], _importCts.Token);
                results.Add(result);
                UpdateProgressRow(rows[index], ToVisualState(result.Disposition));
                UpdateImportProgressHeader(index + 1, paths.Length);
            }

            Refresh();
            ShowImportCompletion(new LibraryImportSummary(results));
        }
        catch (OperationCanceledException)
        {
            Refresh();
            HideImportOverlay();
        }
        finally
        {
            _importRunning = false;
            _importCts?.Dispose();
            _importCts = null;
        }
    }

    private void BuildImportProgress(IReadOnlyList<ImportVisualRow> rows, int completed, int total)
    {
        ImportDialogContentHost.Children.Clear();
        var root = new Grid { Width = 620, Height = 440 };
        ImportDialogContentHost.Children.Add(root);

        var title = TextAt(LocalText("正在导入书籍…", "書籍をインポートしています…", "Importing books…"), 23, 21, 560, 28, 20, true);
        root.Children.Add(title);
        var subtitle = TextAt(FormatProcessingCount(total), 23, 57, 460, 22, 14, false, 0.61);
        root.Children.Add(subtitle);
        var counter = TextAt($"{completed} / {total}", 520, 57, 70, 20, 13, false, 0.56);
        counter.TextAlignment = TextAlignment.Right;
        counter.Name = "ImportCounter";
        root.Children.Add(counter);

        var progress = new ProgressBar
        {
            Name = "ImportOverallProgress",
            Width = 572,
            Height = 5,
            Minimum = 0,
            Maximum = Math.Max(1, total),
            Value = completed,
            Margin = new Thickness(23, 90, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        root.Children.Add(progress);

        var list = new StackPanel { Spacing = 2 };
        foreach (var row in rows)
        {
            var rowGrid = BuildImportProgressRow(row);
            row.Container = rowGrid;
            list.Children.Add(rowGrid);
        }
        var scroll = new ScrollViewer
        {
            Width = 572,
            Height = 250,
            Margin = new Thickness(23, 120, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = list
        };
        root.Children.Add(scroll);

        var cancel = ActionButton(LocalText("取消", "キャンセル", "Cancel"), 92, false);
        cancel.Margin = new Thickness(503, 385, 0, 0);
        cancel.HorizontalAlignment = HorizontalAlignment.Left;
        cancel.VerticalAlignment = VerticalAlignment.Top;
        cancel.Click += (_, _) => _importCts?.Cancel();
        root.Children.Add(cancel);
    }

    private Grid BuildImportProgressRow(ImportVisualRow row)
    {
        var grid = new Grid { Height = 32, CornerRadius = new CornerRadius(4), Tag = row };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

        var name = new TextBlock
        {
            Text = row.FileName,
            FontSize = 13,
            Margin = new Thickness(8, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        grid.Children.Add(name);

        row.StatusText = new TextBlock
        {
            Text = LocalText("等待中", "待機中", "Waiting"),
            FontSize = 12,
            Opacity = 0.45,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(row.StatusText, 1);
        grid.Children.Add(row.StatusText);

        row.GlyphText = new TextBlock
        {
            FontSize = 13,
            Opacity = 0.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(row.GlyphText, 2);
        grid.Children.Add(row.GlyphText);
        return grid;
    }

    private void UpdateProgressRow(ImportVisualRow row, ImportRowState state)
    {
        if (row.StatusText is null || row.GlyphText is null || row.Container is null) return;
        row.Container.Background = state == ImportRowState.Working ? Brush(9, 0, 0, 0) : new SolidColorBrush(Colors.Transparent);
        switch (state)
        {
            case ImportRowState.Working:
                row.StatusText.Text = LocalText("正在读取元数据…", "メタデータを読み込み中…", "Reading metadata…");
                row.StatusText.Opacity = 0.72;
                row.GlyphText.Text = "…";
                row.GlyphText.Foreground = null;
                break;
            case ImportRowState.Added:
                row.StatusText.Text = LocalText("已添加", "追加済み", "Added");
                row.StatusText.Opacity = 0.58;
                row.GlyphText.Text = "✓";
                row.GlyphText.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 26, 140, 64));
                break;
            case ImportRowState.Skipped:
                row.StatusText.Text = LocalText("已跳过", "スキップ", "Skipped");
                row.StatusText.Opacity = 0.58;
                row.GlyphText.Text = "—";
                row.GlyphText.Foreground = null;
                break;
            case ImportRowState.Failed:
                row.StatusText.Text = LocalText("导入失败", "インポート失敗", "Failed");
                row.StatusText.Opacity = 0.72;
                row.GlyphText.Text = "!";
                row.GlyphText.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28));
                break;
        }
    }

    private void UpdateImportProgressHeader(int completed, int total)
    {
        if (ImportDialogContentHost.Children.FirstOrDefault() is not Grid root) return;
        var counter = root.Children.OfType<TextBlock>().FirstOrDefault(x => string.Equals(x.Name, "ImportCounter", StringComparison.Ordinal));
        if (counter is not null) counter.Text = $"{completed} / {total}";
        var progress = root.Children.OfType<ProgressBar>().FirstOrDefault(x => string.Equals(x.Name, "ImportOverallProgress", StringComparison.Ordinal));
        if (progress is not null) progress.Value = completed;
    }

    private void ShowImportCompletion(LibraryImportSummary summary)
    {
        ImportDialogContentHost.Children.Clear();
        var root = new Grid { Width = 620, Height = 440 };
        ImportDialogContentHost.Children.Add(root);

        var badge = new Border
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(28),
            Background = Brush(28, 26, 140, 64),
            Margin = new Thickness(282, 38, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new FontIcon { Glyph = "\uE73E", FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        root.Children.Add(badge);

        var title = TextAt(LocalText("导入完成", "インポート完了", "Import complete"), 0, 117, 620, 34, 22, true);
        title.TextAlignment = TextAlignment.Center;
        root.Children.Add(title);
        var subtitle = TextAt(FormatImportCompletion(summary), 0, 156, 620, 24, 14, false, 0.61);
        subtitle.TextAlignment = TextAlignment.Center;
        root.Children.Add(subtitle);

        var summaryCard = new Border
        {
            Width = 360,
            Height = 88,
            Margin = new Thickness(130, 204, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = Brush(8, 0, 0, 0),
            CornerRadius = new CornerRadius(6)
        };
        var metrics = new Grid();
        metrics.ColumnDefinitions.Add(new ColumnDefinition());
        metrics.ColumnDefinitions.Add(new ColumnDefinition());
        metrics.ColumnDefinitions.Add(new ColumnDefinition());
        AddMetric(metrics, 0, summary.Added.ToString(), LocalText("已添加", "追加", "Added"));
        AddMetric(metrics, 1, (summary.Existing + summary.Unsupported).ToString(), LocalText("已跳过", "スキップ", "Skipped"));
        AddMetric(metrics, 2, summary.Failed.ToString(), LocalText("错误", "エラー", "Errors"));
        summaryCard.Child = metrics;
        root.Children.Add(summaryCard);

        var viewLibrary = ActionButton(LocalText("查看书库", "ライブラリを表示", "View library"), 122, true);
        viewLibrary.Margin = new Thickness(273, 326, 0, 0);
        viewLibrary.HorizontalAlignment = HorizontalAlignment.Left;
        viewLibrary.VerticalAlignment = VerticalAlignment.Top;
        viewLibrary.Click += (_, _) =>
        {
            HideImportOverlay();
            Refresh();
        };
        root.Children.Add(viewLibrary);

        var close = ActionButton(LocalText("关闭", "閉じる", "Close"), 92, false);
        close.Margin = new Thickness(405, 326, 0, 0);
        close.HorizontalAlignment = HorizontalAlignment.Left;
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Click += (_, _) => HideImportOverlay();
        root.Children.Add(close);
    }

    private static ImportRowState ToVisualState(LibraryImportDisposition disposition) => disposition switch
    {
        LibraryImportDisposition.Added => ImportRowState.Added,
        LibraryImportDisposition.ExistingPath or LibraryImportDisposition.DuplicateContent => ImportRowState.Skipped,
        _ => ImportRowState.Failed
    };

    private string FormatProcessingCount(int total) => App.Settings.Current.Language switch
    {
        "zh-CN" => $"正在处理 {total} 个电子书文件",
        "ja-JP" => $"{total} 個の電子書籍ファイルを処理しています",
        _ => $"Processing {total} ebook files"
    };

    private string FormatImportCompletion(LibraryImportSummary summary) => App.Settings.Current.Language switch
    {
        "zh-CN" => $"已将 {summary.Added} 本书添加到 PageArc 书库。",
        "ja-JP" => $"{summary.Added} 冊を PageArc ライブラリに追加しました。",
        _ => $"Added {summary.Added} books to the PageArc library."
    };

    private static void AddMetric(Grid grid, int column, string value, string label)
    {
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 2 };
        stack.Children.Add(new TextBlock { Text = value, FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        stack.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = 0.56 });
        Grid.SetColumn(stack, column);
        grid.Children.Add(stack);
    }

    private static TextBlock TextAt(string text, double left, double top, double width, double height, double size, bool semiBold, double opacity = 1)
    {
        return new TextBlock
        {
            Text = text,
            Width = width,
            Height = height,
            FontSize = size,
            FontWeight = semiBold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            Opacity = opacity,
            Margin = new Thickness(left, top, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }

    private static Button ActionButton(string text, double width, bool accent)
    {
        var button = new Button
        {
            Width = width,
            Height = 34,
            Content = new TextBlock { Text = text, FontSize = 14, FontWeight = accent ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal },
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        if (accent && Application.Current.Resources.TryGetValue("AccentButtonStyle", out var value) && value is Style style)
            button.Style = style;
        return button;
    }

    private static Button IconButton(string glyph, double width, double height) => new()
    {
        Width = width,
        Height = height,
        Padding = new Thickness(0),
        Background = new SolidColorBrush(Colors.Transparent),
        BorderThickness = new Thickness(0),
        Content = new FontIcon { Glyph = glyph, FontSize = 14 }
    };

    private void HideImportOverlay()
    {
        if (_importRunning) _importCts?.Cancel();
        ImportOverlay.Visibility = Visibility.Collapsed;
        ImportDialogContentHost.Children.Clear();
    }

    private static SolidColorBrush Brush(byte alpha, byte r, byte g, byte b) =>
        new(ColorHelper.FromArgb(alpha, r, g, b));

    private enum ImportRowState { Working, Added, Skipped, Failed }

    private sealed class ImportVisualRow
    {
        public ImportVisualRow(string path, string fileName)
        {
            Path = path;
            FileName = fileName;
        }

        public string Path { get; }
        public string FileName { get; }
        public Grid? Container { get; set; }
        public TextBlock? StatusText { get; set; }
        public TextBlock? GlyphText { get; set; }
    }
}
