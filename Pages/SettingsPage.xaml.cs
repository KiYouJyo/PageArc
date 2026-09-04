using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly ReadingBackupService _backupService = new();
    private readonly WebDavSyncService _webDavSyncService = new();
    private readonly WebDavCredentialStore _webDavCredentialStore = new();
    private bool _loaded;
    private bool _webDavBusy;

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
        SelectByTag(ReadingThemeCombo, App.Settings.Current.ReadingThemeFollowsApp ? "app" : App.Settings.Current.ReadingTheme);
        SelectByTag(FontCombo, App.Settings.Current.DefaultFont);
        SelectByTag(LibrarySortCombo, App.Settings.Current.LibrarySort);
        FontScaleSlider.Value = App.Settings.Current.FontScale;
        LineHeightSlider.Value = App.Settings.Current.LineHeight;
        RecentToggle.IsOn = App.Settings.Current.ShowRecentBooks;
        DuplicatesToggle.IsOn = App.Settings.Current.DuplicateDetection;
        UpdateWebDavStatus();
        LanguageCombo.IsEnabled = true;
        _loaded = true;
    }

    private void ApplyExtendedLocalizedText()
    {
        AppearanceSectionHint.Text = LocalText("主题、语言与界面外观", "テーマ、言語、インターフェイスの外観", "Theme, language, and interface appearance");
        LibrarySectionHint.Text = LocalText("管理书库行为与导入偏好", "ライブラリの動作と読み込み設定", "Library behavior and import preferences");
        ReadingSectionHint.Text = LocalText("默认排版与阅读体验", "既定の組版と読書体験", "Default typography and reading experience");
        DataSectionHint.Text = LocalText("本地备份、WebDAV 云存档与本地缓存", "ローカル バックアップ、WebDAV クラウド保存、ローカル キャッシュ", "Local backup, WebDAV cloud archive, and local cache");
        AccentLabel.Text = LocalText("强调色", "アクセント カラー", "Accent color");
        AccentHint.Text = LocalText("使用 Windows 强调色", "Windows のアクセント カラーを使用します", "Use the Windows accent color");
        WindowsAccentItem.Content = "Windows";
        LibrarySortLabel.Text = LocalText("默认排序", "既定の並べ替え", "Default sort");
        LibrarySortRecentItem.Content = LocalText("最近打开", "最近開いた順", "Recently opened");
        LibrarySortTitleItem.Content = LocalText("标题", "タイトル", "Title");

        LocalBackupTitle.Text = LocalText("本地备份", "ローカル バックアップ", "Local backup");
        LocalBackupHint.Text = LocalText("导出或恢复完整 PageArc 备份；包含书本文件、阅读进度、书签、高亮和笔记。", "書籍ファイル、読書位置、しおり、ハイライト、ノートを含む完全な PageArc バックアップを保存・復元します。", "Export or restore a complete PageArc backup with book files, reading positions, bookmarks, highlights, and notes.");
        LocalBackupStatusLabel.Text = LocalText("状态", "状態", "Status");
        BackupReadingDataButton.Content = LocalText("导出备份", "バックアップを保存", "Export backup");
        RestoreReadingDataButton.Content = LocalText("恢复备份", "バックアップを復元", "Restore backup");

        WebDavCardTitle.Text = LocalText("WebDAV 云存档", "WebDAV クラウド保存", "WebDAV cloud archive");
        WebDavHint.Text = LocalText("把书本文件与阅读数据打包为一个存档并双向同步；密码保存在 Windows 凭据保险库。", "書籍ファイルと読書データを 1 つのアーカイブにまとめて双方向同期します。パスワードは Windows 資格情報コンテナーに保存されます。", "Bundle book files and reading data into one archive for two-way sync. Passwords stay in Windows Password Vault.");
        WebDavStatusLabel.Text = LocalText("状态", "状態", "Status");
        WebDavConfigureButton.Content = LocalText("配置", "設定", "Configure");
        WebDavSyncButton.Content = LocalText("立即同步", "今すぐ同期", "Sync now");
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
            if (ReadingThemeCombo.SelectedItem is ComboBoxItem { Tag: string theme })
            {
                settings.ReadingThemeFollowsApp = theme == "app";
                if (theme != "app") settings.ReadingTheme = theme;
            }
            if (FontCombo.SelectedItem is ComboBoxItem { Tag: string font }) settings.DefaultFont = font;
            if (LibrarySortCombo.SelectedItem is ComboBoxItem { Tag: string sort }) settings.LibrarySort = sort;
            settings.FontScale = FontScaleSlider.Value;
            settings.LineHeight = LineHeightSlider.Value;
            settings.ContinuousScrolling = true;
            settings.ShowReadingProgress = true;
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

        try
        {
            await _backupService.ExportPackageAsync(path, App.ReadingData, App.Library.Books);
            UpdateLocalBackupStatus();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Full backup failed", ex);
            await ShowTransientMessageAsync(
                LocalText("备份失败", "バックアップに失敗しました", "Backup failed"),
                LocalText("无法写入完整备份；书本文件和阅读数据均未导出到该位置。", "完全バックアップを書き込めませんでした。書籍ファイルと読書データは選択先に保存されていません。", "PageArc could not write the complete backup to the selected location."));
        }
    }

    private async void RestoreReadingData_Click(object sender, RoutedEventArgs e)
    {
        PersistReadingSettings();
        var path = await PickerService.PickReadingBackupOpenPathAsync();
        if (string.IsNullOrWhiteSpace(path)) return;

        PageArcReadingBackup backup;
        try
        {
            backup = ReadingBackupService.ReadPackage(path);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Backup package could not be read", ex);
            await ShowTransientMessageAsync(
                LocalText("无法读取备份", "バックアップを読み込めません", "Cannot read backup"),
                LocalText("请选择由 PageArc 创建的 .pagearcbackup 备份；旧版 JSON 阅读数据备份也仍可导入。", "PageArc が作成した .pagearcbackup を選択してください。旧形式の JSON 読書データ バックアップも読み込めます。", "Choose a .pagearcbackup created by PageArc. Legacy JSON reading-data backups are also supported."));
            return;
        }

        var confirmation = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = LocalText("恢复 PageArc 备份", "PageArc バックアップを復元", "Restore PageArc backup"),
            Content = LocalText(
                "“合并”会保留本机现有内容，并补回备份中的书本文件与阅读数据；“覆盖”只会覆盖书签和笔记等阅读数据，不会删除本机已有书本。",
                "「マージ」は既存内容を残し、バックアップ内の書籍ファイルと読書データを追加します。「置換」はしおりやノートなどの読書データのみを置き換え、既存の書籍は削除しません。",
                "Merge keeps local content and restores book files plus reading data from the backup. Replace only replaces reading-data items such as bookmarks and notes; it does not delete local books."),
            PrimaryButtonText = LocalText("合并", "マージ", "Merge"),
            SecondaryButtonText = LocalText("覆盖阅读数据", "読書データを置換", "Replace reading data"),
            CloseButtonText = LocalText("取消", "キャンセル", "Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        var choice = await confirmation.ShowAsync();
        if (choice == ContentDialogResult.None) return;

        try
        {
            var restoredBookFiles = await _backupService.RestorePackageBooksAsync(path, backup, App.Library);
            var mode = choice == ContentDialogResult.Primary ? ReadingBackupRestoreMode.Merge : ReadingBackupRestoreMode.Replace;
            var result = _backupService.Restore(backup, App.ReadingData, App.Library.Books, mode);
            App.Library.Save();
            UpdateLocalBackupStatus();

            await ShowTransientMessageAsync(
                LocalText("恢复完成", "復元が完了しました", "Restore complete"),
                LocalText(
                    $"已恢复/接入 {restoredBookFiles} 个书本文件；匹配 {result.MatchedBooks} 本书，恢复 {result.RestoredBookmarks} 个书签、{result.RestoredAnnotations} 条笔记/标注和 {result.RestoredProgress} 条阅读进度；{result.UnmatchedBooks} 本未匹配。",
                    $"書籍ファイル {restoredBookFiles} 件を復元/接続し、{result.MatchedBooks} 冊を照合しました。しおり {result.RestoredBookmarks} 件、ノート/注釈 {result.RestoredAnnotations} 件、読書位置 {result.RestoredProgress} 件を復元。未照合は {result.UnmatchedBooks} 冊です。",
                    $"Restored or reconnected {restoredBookFiles} book files; matched {result.MatchedBooks} books and restored {result.RestoredBookmarks} bookmarks, {result.RestoredAnnotations} notes/annotations, and {result.RestoredProgress} reading positions; {result.UnmatchedBooks} books were not matched."));
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Backup restore failed", ex);
            await ShowTransientMessageAsync(
                LocalText("恢复失败", "復元に失敗しました", "Restore failed"),
                LocalText("备份未完整应用；现有本地书本不会被删除。", "バックアップを完全には適用できませんでした。既存のローカル書籍は削除されません。", "The backup could not be fully applied. Existing local books were not deleted."));
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

    private async void ConfigureWebDav_Click(object sender, RoutedEventArgs e)
    {
        if (_webDavBusy) return;
        PersistReadingSettings();

        var endpointBox = new TextBox
        {
            Header = LocalText("WebDAV 文件夹或存档地址", "WebDAV フォルダーまたはアーカイブ URL", "WebDAV folder or archive URL"),
            PlaceholderText = "https://example.com/dav/PageArc/",
            Text = App.Settings.Current.WebDavEndpoint
        };
        var usernameBox = new TextBox
        {
            Header = LocalText("用户名", "ユーザー名", "Username"),
            Text = App.Settings.Current.WebDavUsername
        };
        var passwordBox = new PasswordBox
        {
            Header = LocalText("密码或应用专用密码", "パスワードまたはアプリ パスワード", "Password or app password"),
            PlaceholderText = string.IsNullOrWhiteSpace(App.Settings.Current.WebDavEndpoint)
                ? string.Empty
                : LocalText("留空以保留已保存密码", "空欄の場合は保存済みパスワードを維持", "Leave blank to keep the saved password")
        };
        var endpointHint = new TextBlock
        {
            Text = LocalText(
                $"可填写 WebDAV 文件夹地址；PageArc 会自动使用 {WebDavConnectionSettings.DefaultArchiveFileName}。也兼容直接填写 .pagearcbackup / .json 文件地址。",
                $"WebDAV フォルダー URL を入力すると、PageArc は {WebDavConnectionSettings.DefaultArchiveFileName} を自動使用します。.pagearcbackup / .json の直接 URL も利用できます。",
                $"Enter a WebDAV folder URL and PageArc will use {WebDavConnectionSettings.DefaultArchiveFileName} automatically. Direct .pagearcbackup or .json URLs are also supported."),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72
        };

        var content = new StackPanel { Spacing = 12, MinWidth = 440 };
        content.Children.Add(endpointBox);
        content.Children.Add(endpointHint);
        content.Children.Add(usernameBox);
        content.Children.Add(passwordBox);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = LocalText("配置 WebDAV 云存档", "WebDAV クラウド保存を設定", "Configure WebDAV cloud archive"),
            Content = content,
            PrimaryButtonText = LocalText("保存并测试", "保存してテスト", "Save and test"),
            CloseButtonText = LocalText("取消", "キャンセル", "Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        WebDavConnectionSettings settings;
        try
        {
            settings = new WebDavConnectionSettings(endpointBox.Text.Trim(), usernameBox.Text.Trim());
            _ = settings.GetEndpointUri();
            _ = settings.GetCollectionUri();
        }
        catch
        {
            await ShowTransientMessageAsync(
                LocalText("地址无效", "URL が無効です", "Invalid address"),
                LocalText("请输入完整的 HTTPS 或 HTTP WebDAV 文件夹/文件地址。", "完全な HTTPS または HTTP の WebDAV フォルダー/ファイル URL を入力してください。", "Enter a complete HTTPS or HTTP WebDAV folder/file URL."));
            return;
        }

        var previousEndpoint = App.Settings.Current.WebDavEndpoint;
        var previousUsername = App.Settings.Current.WebDavUsername;
        var password = passwordBox.Password;
        if (string.IsNullOrWhiteSpace(password)
            && string.Equals(previousEndpoint, settings.Endpoint, StringComparison.OrdinalIgnoreCase)
            && string.Equals(previousUsername, settings.Username, StringComparison.Ordinal))
        {
            password = _webDavCredentialStore.Read(previousEndpoint, previousUsername) ?? string.Empty;
        }

        App.Settings.Update(value =>
        {
            value.WebDavEndpoint = settings.Endpoint;
            value.WebDavUsername = settings.Username;
        });
        if (!string.IsNullOrWhiteSpace(passwordBox.Password))
            _webDavCredentialStore.Save(settings.Endpoint, settings.Username, passwordBox.Password);

        SetWebDavBusy(true);
        try
        {
            await _webDavSyncService.TestConnectionAsync(settings, password);
            WebDavStatusText.Text = LocalText("已连接 · 配置已保存", "接続済み · 設定を保存しました", "Connected · configuration saved");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("WebDAV connection test failed", ex);
            WebDavStatusText.Text = LocalText("连接测试失败 · 请检查文件夹地址和凭据", "接続テスト失敗 · フォルダー URL と資格情報を確認してください", "Connection test failed · check the folder URL and credentials");
        }
        finally { SetWebDavBusy(false); }
    }

    private async void SyncWebDav_Click(object sender, RoutedEventArgs e)
    {
        if (_webDavBusy) return;
        PersistReadingSettings();

        var settings = new WebDavConnectionSettings(App.Settings.Current.WebDavEndpoint, App.Settings.Current.WebDavUsername);
        try
        {
            _ = settings.GetEndpointUri();
            _ = settings.GetCollectionUri();
        }
        catch
        {
            await ShowTransientMessageAsync(
                LocalText("尚未配置 WebDAV", "WebDAV は未設定です", "WebDAV is not configured"),
                LocalText("请先配置 WebDAV 文件夹地址和凭据。", "先に WebDAV フォルダー URL と資格情報を設定してください。", "Configure the WebDAV folder URL and credentials first."));
            return;
        }

        var remotePath = Path.Combine(Path.GetTempPath(), $"PageArc-remote-{Guid.NewGuid():N}{ReadingBackupService.PackageExtension}");
        var uploadPath = Path.Combine(Path.GetTempPath(), $"PageArc-upload-{Guid.NewGuid():N}{ReadingBackupService.PackageExtension}");

        SetWebDavBusy(true);
        WebDavStatusText.Text = LocalText("正在下载、合并并同步书本与阅读数据…", "書籍と読書データをダウンロード、マージ、同期しています…", "Downloading, merging, and syncing books plus reading data…");
        try
        {
            var password = _webDavCredentialStore.Read(settings.Endpoint, settings.Username) ?? string.Empty;
            var remoteExists = await _webDavSyncService.DownloadFileAsync(settings, password, remotePath);

            if (remoteExists)
            {
                var remote = ReadingBackupService.ReadPackage(remotePath);
                await _backupService.RestorePackageBooksAsync(remotePath, remote, App.Library);

                var localAfterBooks = _backupService.CreateBackup(App.ReadingData, App.Library.Books);
                var merged = ReadingBackupService.Merge(localAfterBooks, remote);
                _backupService.Restore(merged, App.ReadingData, App.Library.Books, ReadingBackupRestoreMode.Merge);
                App.Library.Save();
            }

            await _backupService.ExportPackageAsync(uploadPath, App.ReadingData, App.Library.Books);
            await _webDavSyncService.UploadFileAsync(settings, password, uploadPath);

            var now = DateTimeOffset.Now;
            App.Settings.Update(value => value.WebDavLastSyncAt = now);
            UpdateWebDavStatus();
            UpdateLocalBackupStatus();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("WebDAV sync failed", ex);
            WebDavStatusText.Text = LocalText(
                "同步失败 · 本地书本与阅读数据未被删除",
                "同期失敗 · ローカルの書籍と読書データは削除されていません",
                "Sync failed · local books and reading data were not deleted");
        }
        finally
        {
            TryDelete(remotePath);
            TryDelete(uploadPath);
            SetWebDavBusy(false);
        }
    }

    private void UpdateWebDavStatus()
    {
        UpdateLocalBackupStatus();

        if (string.IsNullOrWhiteSpace(App.Settings.Current.WebDavEndpoint))
        {
            WebDavStatusText.Text = LocalText("未配置", "未設定", "Not configured");
        }
        else if (App.Settings.Current.WebDavLastSyncAt is DateTimeOffset lastSync)
        {
            WebDavStatusText.Text = string.Format(
                LocalText("已连接 · 上次同步 {0:yyyy-MM-dd HH:mm}", "接続済み · 最終同期 {0:yyyy-MM-dd HH:mm}", "Connected · last synced {0:yyyy-MM-dd HH:mm}"),
                lastSync.ToLocalTime());
        }
        else
        {
            WebDavStatusText.Text = LocalText("已配置 · 尚未完成首次同步", "設定済み · 初回同期前", "Configured · first sync pending");
        }

        WebDavSyncButton.IsEnabled = !_webDavBusy && !string.IsNullOrWhiteSpace(App.Settings.Current.WebDavEndpoint);
    }

    private void UpdateLocalBackupStatus()
    {
        var total = App.Library.Books.Count;
        var available = App.Library.Books.Count(book => !book.IsMissing && File.Exists(book.FilePath));
        LocalBackupStatusText.Text = string.Format(
            LocalText("本地数据正常 · {0}/{1} 本书文件可用", "ローカル データ正常 · 書籍ファイル {0}/{1} 件利用可能", "Local data healthy · {0}/{1} book files available"),
            available,
            total);
    }

    private void SetWebDavBusy(bool busy)
    {
        _webDavBusy = busy;
        WebDavProgressRing.IsActive = busy;
        WebDavProgressRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        WebDavConfigureButton.IsEnabled = !busy;
        WebDavSyncButton.IsEnabled = !busy && !string.IsNullOrWhiteSpace(App.Settings.Current.WebDavEndpoint);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Temporary sync files are best-effort cleanup only.
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
