using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly ReadingBackupService _backupService = new();
    private bool _loaded;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
        Unloaded += (_, _) => PersistReadingSettings();
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyExtendedLocalizedText();
        SelectByTag(LanguageCombo, App.Settings.Current.Language);
        SelectByTag(ThemeCombo, App.Settings.Current.AppTheme);
        SelectByTag(AccentCombo, App.Settings.Current.AccentSource);
        SelectByTag(ReadingThemeCombo, App.Settings.Current.ReadingTheme);
        SelectByTag(FontCombo, App.Settings.Current.DefaultFont);
        SelectByTag(PageWidthCombo, App.Settings.Current.PageWidth);
        SelectByTag(LibrarySortCombo, App.Settings.Current.LibrarySort);
        FontScaleSlider.Value = App.Settings.Current.FontScale;
        LineHeightSlider.Value = App.Settings.Current.LineHeight;
        ContinuousToggle.IsOn = App.Settings.Current.ContinuousScrolling;
        RecentToggle.IsOn = App.Settings.Current.ShowRecentBooks;
        DuplicatesToggle.IsOn = App.Settings.Current.DuplicateDetection;
        LanguageCombo.IsEnabled = true;
        _loaded = true;
    }

    private void ApplyExtendedLocalizedText()
    {
        AccentLabel.Text = LocalText("强调色", "アクセント カラー", "Accent color");
        AccentHint.Text = LocalText("使用 Windows 强调色", "Windows のアクセント カラーを使用します", "Use the Windows accent color");
        WindowsAccentItem.Content = "Windows";
        PageWidthLabel.Text = LocalText("页面宽度", "ページ幅", "Page width");
        PageWidthNarrowItem.Content = LocalText("窄", "狭い", "Narrow");
        PageWidthMediumItem.Content = LocalText("中", "中", "Medium");
        PageWidthWideItem.Content = LocalText("宽", "広い", "Wide");
        LibrarySortLabel.Text = LocalText("默认排序", "既定の並べ替え", "Default sort");
        LibrarySortRecentItem.Content = LocalText("最近打开", "最近開いた順", "Recently opened");
        LibrarySortTitleItem.Content = LocalText("标题", "タイトル", "Title");
        RestoreReadingDataButton.Content = LocalText("恢复", "復元", "Restore");
    }

    private static void SelectByTag(ComboBox comboBox, string tag)
    {
        comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            ?? comboBox.Items.FirstOrDefault();
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || LanguageCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        if (LanguagePreference.Normalize(tag) == App.Settings.Current.Language) return;
        PersistReadingSettings();
        LanguageCombo.IsEnabled = false;
        try
        {
            var switched = App.Localization.SwitchLanguage(tag);
            if (!switched) SelectByTag(LanguageCombo, App.Settings.Current.Language);
        }
        finally { LanguageCombo.IsEnabled = true; }
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || ThemeCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        if (string.Equals(tag, App.Settings.Current.AppTheme, StringComparison.OrdinalIgnoreCase)) return;
        App.Settings.Update(x => x.AppTheme = tag);
        App.MainWindow?.ApplyAppTheme(tag);
    }

    private void PersistReadingSettings()
    {
        if (!_loaded) return;
        App.Settings.Update(settings =>
        {
            if (AccentCombo.SelectedItem is ComboBoxItem { Tag: string accent }) settings.AccentSource = accent;
            if (ReadingThemeCombo.SelectedItem is ComboBoxItem { Tag: string theme }) settings.ReadingTheme = theme;
            if (FontCombo.SelectedItem is ComboBoxItem { Tag: string font }) settings.DefaultFont = font;
            if (PageWidthCombo.SelectedItem is ComboBoxItem { Tag: string pageWidth }) settings.PageWidth = pageWidth;
            if (LibrarySortCombo.SelectedItem is ComboBoxItem { Tag: string sort }) settings.LibrarySort = sort;
            settings.FontScale = FontScaleSlider.Value;
            settings.LineHeight = LineHeightSlider.Value;
            settings.ContinuousScrolling = ContinuousToggle.IsOn;
            settings.ShowRecentBooks = RecentToggle.IsOn;
            settings.DuplicateDetection = DuplicatesToggle.IsOn;
        });
        App.Library.DuplicateDetectionEnabled = DuplicatesToggle.IsOn;
    }

    private void ManageFolders_Click(object sender, RoutedEventArgs e)
    {
        PersistReadingSettings();
        App.MainWindow?.NavigateTo("import-folders");
    }

    private async void BackupReadingData_Click(object sender, RoutedEventArgs e)
    {
        PersistReadingSettings();
        var path = await PickerService.PickReadingBackupSavePathAsync();
        if (string.IsNullOrWhiteSpace(path)) return;
        try { await _backupService.ExportAsync(path, App.ReadingData, App.Library.Books); }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Reading data backup failed", ex);
            await ShowTransientMessageAsync(LocalText("备份失败", "バックアップに失敗しました", "Backup failed"), LocalText("无法写入所选位置。", "選択した場所に書き込めませんでした。", "PageArc could not write to the selected location."));
        }
    }

    private async void RestoreReadingData_Click(object sender, RoutedEventArgs e)
    {
        PersistReadingSettings();
        var path = await PickerService.PickReadingBackupOpenPathAsync();
        if (string.IsNullOrWhiteSpace(path)) return;

        PageArcReadingBackup backup;
        try { backup = ReadingBackupService.Read(path); }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Reading data backup could not be read", ex);
            await ShowTransientMessageAsync(LocalText("无法读取备份", "バックアップを読み込めません", "Cannot read backup"), LocalText("请选择由 PageArc 创建的有效阅读数据备份。", "PageArc が作成した有効な読書データ バックアップを選択してください。", "Choose a valid reading-data backup created by PageArc."));
            return;
        }

        var confirmation = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = LocalText("恢复阅读数据", "読書データを復元", "Restore reading data"),
            Content = LocalText(
                "“合并”会保留本机已有项目，并导入备份；“覆盖”会用备份替换本机书签和笔记。两种模式都会恢复匹配书籍的阅读进度。",
                "「マージ」は既存データを残してバックアップを取り込みます。「置換」はローカルのしおりとノートをバックアップで置き換えます。どちらも一致した本の読書位置を復元します。",
                "Merge keeps local items and imports the backup. Replace overwrites local bookmarks and notes with the backup. Both restore progress for matched books."),
            PrimaryButtonText = LocalText("合并", "マージ", "Merge"),
            SecondaryButtonText = LocalText("覆盖", "置換", "Replace"),
            CloseButtonText = LocalText("取消", "キャンセル", "Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        var choice = await confirmation.ShowAsync();
        if (choice == ContentDialogResult.None) return;

        try
        {
            var mode = choice == ContentDialogResult.Primary ? ReadingBackupRestoreMode.Merge : ReadingBackupRestoreMode.Replace;
            var result = _backupService.Restore(backup, App.ReadingData, App.Library.Books, mode);
            App.Library.Save();
            await ShowTransientMessageAsync(
                LocalText("恢复完成", "復元が完了しました", "Restore complete"),
                LocalText(
                    $"已匹配 {result.MatchedBooks} 本书，恢复 {result.RestoredBookmarks} 个书签、{result.RestoredAnnotations} 条笔记/标注和 {result.RestoredProgress} 条阅读进度；{result.UnmatchedBooks} 本未匹配。",
                    $"{result.MatchedBooks} 冊を照合し、しおり {result.RestoredBookmarks} 件、ノート/注釈 {result.RestoredAnnotations} 件、読書位置 {result.RestoredProgress} 件を復元しました。未照合は {result.UnmatchedBooks} 冊です。",
                    $"Matched {result.MatchedBooks} books and restored {result.RestoredBookmarks} bookmarks, {result.RestoredAnnotations} notes/annotations, and {result.RestoredProgress} reading positions; {result.UnmatchedBooks} books were not matched."));
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Reading data restore failed", ex);
            await ShowTransientMessageAsync(LocalText("恢复失败", "復元に失敗しました", "Restore failed"), LocalText("备份未被应用。", "バックアップは適用されませんでした。", "The backup could not be applied."));
        }
    }

    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var changed = CacheMaintenanceService.ClearGeneratedCache(App.Library.Books);
            if (changed > 0) App.Library.Save();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Cache clear failed", ex);
            await ShowTransientMessageAsync(LocalText("清理缓存失败", "キャッシュのクリアに失敗しました", "Clear cache failed"), LocalText("部分缓存文件可能仍被占用。", "一部のキャッシュ ファイルが使用中の可能性があります。", "Some cache files may still be in use."));
        }
    }

    private async Task ShowTransientMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = message, CloseButtonText = LocalText("关闭", "閉じる", "Close") };
        await dialog.ShowAsync();
    }

    private static string LocalText(string zh, string ja, string en)
    {
        var language = App.Localization.CurrentLanguage;
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return zh;
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return ja;
        return en;
    }
}
