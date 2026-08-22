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
        DataSectionHint.Text = LocalText("备份、WebDAV 同步与本地缓存", "バックアップ、WebDAV 同期、ローカル キャッシュ", "Backup, WebDAV sync, and local cache");
        AccentLabel.Text = LocalText("强调色", "アクセント カラー", "Accent color");
        AccentHint.Text = LocalText("使用 Windows 强调色", "Windows のアクセント カラーを使用します", "Use the Windows accent color");
        WindowsAccentItem.Content = "Windows";
        LibrarySortLabel.Text = LocalText("默认排序", "既定の並べ替え", "Default sort");
        LibrarySortRecentItem.Content = LocalText("最近打开", "最近開いた順", "Recently opened");
        LibrarySortTitleItem.Content = LocalText("标题", "タイトル", "Title");
        RestoreReadingDataButton.Content = LocalText("恢复", "復元", "Restore");
        WebDavLabel.Text = "WebDAV";
        WebDavHint.Text = LocalText("同步阅读进度、书签、高亮和笔记；密码安全保存在 Windows 凭据保险库", "読書位置、しおり、ハイライト、ノートを同期します。パスワードは Windows 資格情報コンテナーに安全に保存されます", "Sync reading positions, bookmarks, highlights, and notes. Passwords are stored securely in Windows Password Vault");
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

    private async void ConfigureWebDav_Click(object sender, RoutedEventArgs e)
    {
        if (_webDavBusy) return;
        PersistReadingSettings();

        var endpointBox = new TextBox
        {
            Header = LocalText("WebDAV 文件地址", "WebDAV ファイル URL", "WebDAV file URL"),
            PlaceholderText = "https://example.com/dav/PageArc/reading-data.json",
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
        var content = new StackPanel { Spacing = 12, MinWidth = 420 };
        content.Children.Add(endpointBox);
        content.Children.Add(usernameBox);
        content.Children.Add(passwordBox);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = LocalText("配置 WebDAV 同步", "WebDAV 同期を設定", "Configure WebDAV sync"),
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
        }
        catch (Exception)
        {
            await ShowTransientMessageAsync(LocalText("地址无效", "URL が無効です", "Invalid address"), LocalText("请输入完整的 HTTPS 或 HTTP WebDAV 文件地址。", "完全な HTTPS または HTTP WebDAV ファイル URL を入力してください。", "Enter a complete HTTPS or HTTP WebDAV file URL."));
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
            WebDavStatusText.Text = LocalText("连接成功，配置已保存", "接続成功。設定を保存しました", "Connected. Configuration saved");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("WebDAV connection test failed", ex);
            WebDavStatusText.Text = LocalText("连接测试失败，请检查地址和凭据", "接続テストに失敗しました。URL と資格情報を確認してください", "Connection test failed. Check the URL and credentials");
        }
        finally { SetWebDavBusy(false); }
    }

    private async void SyncWebDav_Click(object sender, RoutedEventArgs e)
    {
        if (_webDavBusy) return;
        PersistReadingSettings();
        var settings = new WebDavConnectionSettings(App.Settings.Current.WebDavEndpoint, App.Settings.Current.WebDavUsername);
        try { _ = settings.GetEndpointUri(); }
        catch
        {
            await ShowTransientMessageAsync(LocalText("尚未配置 WebDAV", "WebDAV は未設定です", "WebDAV is not configured"), LocalText("请先配置 WebDAV 文件地址和凭据。", "先に WebDAV ファイル URL と資格情報を設定してください。", "Configure the WebDAV file URL and credentials first."));
            return;
        }

        SetWebDavBusy(true);
        WebDavStatusText.Text = LocalText("正在合并并同步…", "マージして同期しています…", "Merging and syncing…");
        try
        {
            var password = _webDavCredentialStore.Read(settings.Endpoint, settings.Username) ?? string.Empty;
            var local = _backupService.CreateBackup(App.ReadingData, App.Library.Books);
            var remote = await _webDavSyncService.DownloadAsync(settings, password);
            var merged = remote is null ? local : ReadingBackupService.Merge(local, remote);
            if (remote is not null)
            {
                _backupService.Restore(merged, App.ReadingData, App.Library.Books, ReadingBackupRestoreMode.Merge);
                App.Library.Save();
                merged = _backupService.CreateBackup(App.ReadingData, App.Library.Books);
            }
            await _webDavSyncService.UploadAsync(settings, password, merged);
            WebDavStatusText.Text = string.Format(LocalText("上次同步：{0:HH:mm}", "最終同期：{0:HH:mm}", "Last synced: {0:HH:mm}"), DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("WebDAV sync failed", ex);
            WebDavStatusText.Text = LocalText("同步失败，本地数据未被覆盖", "同期に失敗しました。ローカル データは上書きされていません", "Sync failed. Local data was not overwritten");
        }
        finally { SetWebDavBusy(false); }
    }

    private void UpdateWebDavStatus()
    {
        WebDavStatusText.Text = string.IsNullOrWhiteSpace(App.Settings.Current.WebDavEndpoint)
            ? LocalText("未配置", "未設定", "Not configured")
            : LocalText("已配置", "設定済み", "Configured");
        WebDavSyncButton.IsEnabled = !_webDavBusy && !string.IsNullOrWhiteSpace(App.Settings.Current.WebDavEndpoint);
    }

    private void SetWebDavBusy(bool busy)
    {
        _webDavBusy = busy;
        WebDavProgressRing.IsActive = busy;
        WebDavProgressRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        WebDavConfigureButton.IsEnabled = !busy;
        WebDavSyncButton.IsEnabled = !busy && !string.IsNullOrWhiteSpace(App.Settings.Current.WebDavEndpoint);
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
