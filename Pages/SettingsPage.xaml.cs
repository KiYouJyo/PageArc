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
        AccentLabel.Text = LocalText("强调色", "アクセント カラー", "Accent color");
        AccentHint.Text = LocalText("使用 Windows 强调色", "Windows のアクセント カラーを使用します", "Use the Windows accent color");
        WindowsAccentItem.Content = "Windows";
        LibrarySortLabel.Text = LocalText("默认排序", "既定の並べ替え", "Default sort");
        LibrarySortRecentItem.Content = LocalText("最近打开", "最近開いた順", "Recently opened");
        LibrarySortTitleItem.Content = LocalText("标题", "タイトル", "Title");

        DataManagementTitle.Text = LocalText("数据管理", "データ管理", "Data management");
        DataManagementDescription.Text = LocalText(
            "把本地备份与 WebDAV 云存档并列呈现；本地数据仍是唯一主数据源。",
            "ローカル バックアップと WebDAV クラウド保存を並列表示します。ローカル データが引き続き唯一の主データです。",
            "Local backup and WebDAV cloud archive are presented side by side; local data remains the single source of truth.");

        LocalBackupTitle.Text = LocalText("本地备份", "ローカル バックアップ", "Local backup");
        LocalBackupDescription.Text = LocalText(
            "导出或恢复完整 .pagearcbackup；书本文件与阅读数据会一起打包。",
            "完全な .pagearcbackup を保存または復元します。書籍ファイルと読書データは一緒にパッケージ化されます。",
            "Export or restore a complete .pagearcbackup; book files and reading data are bundled together.");
        LocalBackupStatusLabel.Text = LocalText("状态", "状態", "Status");
        ExportButton.Content = LocalText("导出数据", "データを書き出す", "Export data");
        ImportButton.Content = LocalText("导入数据", "データを読み込む", "Import data");
        ClearDataButton.Content = LocalText("清除缓存", "キャッシュを消去", "Clear cache");

        WebDavTitle.Text = LocalText("WebDAV 云存档", "WebDAV クラウド保存", "WebDAV cloud archive");
        WebDavDescription.Text = LocalText(
            "使用完整备份包双向同步书本与阅读数据；凭据由 Windows Credential Locker 保存。",
            "完全バックアップ パッケージで書籍と読書データを双方向同期します。資格情報は Windows Credential Locker に保存されます。",
            "Two-way sync books and reading data with the complete backup package; credentials are stored in Windows Credential Locker.");
        WebDavStatusLabel.Text = LocalText("状态", "状態", "Status");
        WebDavBackupButton.Content = LocalText("立即同步", "今すぐ同期", "Sync now");
        WebDavRestoreButton.Content = LocalText("从云端恢复", "クラウドから復元", "Restore from cloud");
        WebDavManageButton.Content = LocalText("管理存档", "アーカイブを管理", "Manage archive");
        WebDavConfigureButton.Content = LocalText("配置", "設定", "Configure");
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
            SetDataStatus(
                LocalText("完整备份已导出。", "完全バックアップを書き出しました。", "Complete backup exported."),
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Full backup failed", ex);
            SetDataStatus(
                LocalText("备份失败：无法写入所选位置。", "バックアップ失敗：選択した場所に書き込めません。", "Backup failed: PageArc could not write to the selected location."),
                InfoBarSeverity.Error);
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

            SetDataStatus(
                LocalText(
                    $"恢复完成：接入 {restoredBookFiles} 个书本文件，恢复 {result.RestoredBookmarks} 个书签、{result.RestoredAnnotations} 条标注/笔记和 {result.RestoredProgress} 条阅读进度。",
                    $"復元完了：書籍ファイル {restoredBookFiles} 件、しおり {result.RestoredBookmarks} 件、注釈/ノート {result.RestoredAnnotations} 件、読書位置 {result.RestoredProgress} 件を復元しました。",
                    $"Restore complete: {restoredBookFiles} book files, {result.RestoredBookmarks} bookmarks, {result.RestoredAnnotations} annotations/notes, and {result.RestoredProgress} reading positions restored."),
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Backup restore failed", ex);
            SetDataStatus(
                LocalText("恢复失败：现有本地书本不会被删除。", "復元失敗：既存のローカル書籍は削除されません。", "Restore failed: existing local books were not deleted."),
                InfoBarSeverity.Error);
        }
    }

    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var changed = CacheMaintenanceService.ClearGeneratedCache(App.Library.Books);
            if (changed > 0) App.Library.Save();
            UpdateLocalBackupStatus();
            SetDataStatus(
                LocalText("生成缓存已清除，书本原文件与阅读数据未删除。", "生成キャッシュを消去しました。書籍ファイルと読書データは削除されていません。", "Generated cache cleared. Book files and reading data were not deleted."),
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Cache clear failed", ex);
            SetDataStatus(
                LocalText("清理缓存失败：部分缓存文件可能仍被占用。", "キャッシュ消去失敗：一部のファイルが使用中の可能性があります。", "Clear cache failed: some generated files may still be in use."),
                InfoBarSeverity.Error);
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
                "建议填写 WebDAV 文件夹地址；PageArc 会按时间与版本创建可管理的 .pagearcbackup 历史存档。仍兼容直接填写单个 .pagearcbackup / .json 地址。",
                "WebDAV フォルダー URL の使用を推奨します。PageArc は日時とバージョン付きの .pagearcbackup 履歴を作成・管理します。単一の .pagearcbackup / .json URL も互換用に利用できます。",
                "A WebDAV folder URL is recommended. PageArc creates manageable timestamped/versioned .pagearcbackup history there. Direct single .pagearcbackup / .json URLs remain supported for compatibility."),
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
            WebDavStatusValue.Text = LocalText("已连接 · 配置已保存", "接続済み · 設定を保存しました", "Connected · configuration saved");
            SetWebDavStatus(
                LocalText("WebDAV 配置已保存并通过连接测试。", "WebDAV 設定を保存し、接続テストに成功しました。", "WebDAV configuration saved and connection test passed."),
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("WebDAV connection test failed", ex);
            WebDavStatusValue.Text = LocalText("连接测试失败 · 请检查文件夹地址和凭据", "接続テスト失敗 · フォルダー URL と資格情報を確認してください", "Connection test failed · check the folder URL and credentials");
            SetWebDavStatus(
                LocalText("WebDAV 连接测试失败。", "WebDAV 接続テストに失敗しました。", "WebDAV connection test failed."),
                InfoBarSeverity.Error);
        }
        finally { SetWebDavBusy(false); }
    }

    private async void SyncWebDav_Click(object sender, RoutedEventArgs e)
    {
        if (_webDavBusy) return;
        PersistReadingSettings();

        if (!TryGetWebDavSettings(out var settings))
        {
            SetWebDavStatus(
                LocalText("请先配置 WebDAV 文件夹地址和凭据。", "先に WebDAV フォルダー URL と資格情報を設定してください。", "Configure the WebDAV folder URL and credentials first."),
                InfoBarSeverity.Warning);
            return;
        }

        var localPath = Path.Combine(Path.GetTempPath(), $"PageArc-local-{Guid.NewGuid():N}{ReadingBackupService.PackageExtension}");
        var remotePath = Path.Combine(Path.GetTempPath(), $"PageArc-remote-{Guid.NewGuid():N}{ReadingBackupService.PackageExtension}");
        var mergedPath = Path.Combine(Path.GetTempPath(), $"PageArc-merged-{Guid.NewGuid():N}{ReadingBackupService.PackageExtension}");

        SetWebDavBusy(true);
        BeginWebDavProgress(LocalText("正在检查云端存档…", "クラウド アーカイブを確認しています…", "Checking cloud archives…"));
        try
        {
            var password = _webDavCredentialStore.Read(settings.Endpoint, settings.Username) ?? string.Empty;

            SetWebDavProgress(6, LocalText("正在读取云端存档列表…", "クラウド アーカイブ一覧を取得しています…", "Reading cloud archive list…"));
            var listing = await _webDavSyncService.ListArchivesAsync(settings, password);
            if (!listing.Succeeded)
                throw new InvalidOperationException($"WebDAV archive listing failed: {listing.ErrorCode}");

            SetWebDavProgress(14, LocalText("正在生成本地完整快照…", "ローカルの完全スナップショットを作成しています…", "Creating local complete snapshot…"));
            await _backupService.ExportPackageAsync(localPath, App.ReadingData, App.Library.Books);

            if (listing.Items.Count == 0)
            {
                SetWebDavProgress(34, LocalText("云端暂无存档，正在上传首个完整存档…", "クラウドにアーカイブがありません。最初の完全アーカイブをアップロードしています…", "No cloud archive exists; uploading the first complete archive…"));
                var newName = CreateCloudArchiveFileName();
                await _webDavSyncService.UploadArchiveAsync(
                    settings,
                    password,
                    localPath,
                    newName,
                    CreateMappedTransferProgress(34, 98, LocalText("正在上传完整存档…", "完全アーカイブをアップロードしています…", "Uploading complete archive…")));
                CompleteWebDavSync(
                    LocalText("首次云存档已创建；书本与阅读数据已同步。", "最初のクラウド アーカイブを作成し、書籍と読書データを同期しました。", "First cloud archive created; books and reading data are synchronized."));
                return;
            }

            var latest = listing.Items[0];
            SetWebDavProgress(
                24,
                string.Format(
                    LocalText("正在下载并比较最新云存档：{0}", "最新のクラウド アーカイブをダウンロードして比較しています：{0}", "Downloading and comparing latest cloud archive: {0}"),
                    latest.FileName));

            var downloaded = await _webDavSyncService.DownloadArchiveAsync(
                settings,
                password,
                latest,
                remotePath,
                CreateMappedTransferProgress(24, 50, LocalText("正在下载最新云存档…", "最新のクラウド アーカイブをダウンロードしています…", "Downloading latest cloud archive…")));
            if (!downloaded)
                throw new FileNotFoundException("The selected remote archive disappeared during synchronization.", latest.FileName);

            SetWebDavProgress(54, LocalText("正在检查本地与云端差异…", "ローカルとクラウドの差分を確認しています…", "Checking local/cloud differences…"));
            var localHash = await ReadingBackupService.ComputePackageContentHashAsync(localPath);
            var remoteHash = await ReadingBackupService.ComputePackageContentHashAsync(remotePath);

            if (string.Equals(localHash, remoteHash, StringComparison.Ordinal))
            {
                CompleteWebDavSync(
                    LocalText("已检查：本地与云端没有差异，未上传新存档。", "確認完了：ローカルとクラウドに差分がないため、新しいアーカイブはアップロードしていません。", "Checked: local and cloud data are identical; no new archive was uploaded."));
                return;
            }

            SetWebDavProgress(62, LocalText("检测到差异，正在合并云端与本地数据…", "差分を検出しました。クラウドとローカルのデータをマージしています…", "Differences found; merging cloud and local data…"));
            var remote = ReadingBackupService.ReadPackage(remotePath);
            await _backupService.RestorePackageBooksAsync(remotePath, remote, App.Library);

            var localAfterBooks = _backupService.CreateBackup(App.ReadingData, App.Library.Books);
            var merged = ReadingBackupService.Merge(localAfterBooks, remote);
            _backupService.Restore(merged, App.ReadingData, App.Library.Books, ReadingBackupRestoreMode.Merge);
            App.Library.Save();

            SetWebDavProgress(70, LocalText("正在生成合并后的完整存档…", "マージ後の完全アーカイブを作成しています…", "Creating merged complete archive…"));
            await _backupService.ExportPackageAsync(mergedPath, App.ReadingData, App.Library.Books);
            var mergedHash = await ReadingBackupService.ComputePackageContentHashAsync(mergedPath);

            if (string.Equals(mergedHash, remoteHash, StringComparison.Ordinal))
            {
                CompleteWebDavSync(
                    LocalText("已从云端合并更新到本地；云端内容已包含全部数据，因此未重复上传。", "クラウドの内容をローカルへ反映しました。クラウド側に全データが含まれているため再アップロードしていません。", "Cloud changes were merged locally; the cloud already contained the complete result, so no duplicate upload was made."));
                return;
            }

            SetWebDavProgress(76, LocalText("合并结果有新内容，正在创建新的云存档…", "マージ結果に新しい内容があります。新しいクラウド アーカイブを作成しています…", "Merged result contains new data; creating a new cloud archive…"));
            var archiveName = CreateCloudArchiveFileName();
            await _webDavSyncService.UploadArchiveAsync(
                settings,
                password,
                mergedPath,
                archiveName,
                CreateMappedTransferProgress(76, 98, LocalText("正在上传新的完整存档…", "新しい完全アーカイブをアップロードしています…", "Uploading new complete archive…")));

            CompleteWebDavSync(
                LocalText("差异已合并，并创建新的云存档。", "差分をマージし、新しいクラウド アーカイブを作成しました。", "Differences merged and a new cloud archive was created."));
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("WebDAV sync failed", ex);
            WebDavStatusValue.Text = LocalText(
                "同步失败 · 本地书本与阅读数据未被删除",
                "同期失敗 · ローカルの書籍と読書データは削除されていません",
                "Sync failed · local books and reading data were not deleted");
            SetWebDavStatus(
                LocalText("同步失败；本地数据保持不变。", "同期に失敗しました。ローカル データは保持されています。", "Sync failed; local data was preserved."),
                InfoBarSeverity.Error);
        }
        finally
        {
            TryDelete(localPath);
            TryDelete(remotePath);
            TryDelete(mergedPath);
            EndWebDavProgress();
            SetWebDavBusy(false);
        }
    }

    private async void RestoreWebDav_Click(object sender, RoutedEventArgs e)
    {
        if (_webDavBusy) return;
        PersistReadingSettings();

        if (!TryGetWebDavSettings(out var settings))
        {
            SetWebDavStatus(
                LocalText("请先配置 WebDAV。", "先に WebDAV を設定してください。", "Configure WebDAV first."),
                InfoBarSeverity.Warning);
            return;
        }

        SetWebDavBusy(true);
        BeginWebDavProgress(LocalText("正在读取云端存档列表…", "クラウド アーカイブ一覧を取得しています…", "Reading cloud archive list…"));
        try
        {
            var password = _webDavCredentialStore.Read(settings.Endpoint, settings.Username) ?? string.Empty;
            var listing = await _webDavSyncService.ListArchivesAsync(settings, password);
            EndWebDavProgress();

            if (!listing.Succeeded)
            {
                SetWebDavStatus(
                    LocalText("无法读取云端存档列表。", "クラウド アーカイブ一覧を取得できません。", "Could not read the cloud archive list."),
                    InfoBarSeverity.Error);
                return;
            }

            if (listing.Items.Count == 0)
            {
                SetWebDavStatus(
                    LocalText("云端暂无存档。", "クラウド アーカイブはまだありません。", "No cloud archives yet."),
                    InfoBarSeverity.Informational);
                return;
            }

            var selected = await SelectBackupAsync(
                listing.Items,
                LocalText("选择要恢复的云存档", "復元するクラウドアーカイブを選択", "Choose a cloud archive to restore"),
                LocalText("恢复", "復元", "Restore"));
            if (selected is null) return;

            _ = await RestoreBackupAsync(settings, password, selected);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("WebDAV cloud restore failed", ex);
            SetWebDavStatus(
                LocalText("云端恢复失败；本地现有数据未被删除。", "クラウド復元に失敗しました。既存のローカル データは削除されていません。", "Cloud restore failed; existing local data was not deleted."),
                InfoBarSeverity.Error);
        }
        finally
        {
            EndWebDavProgress();
            SetWebDavBusy(false);
        }
    }

    private async void ManageWebDav_Click(object sender, RoutedEventArgs e)
    {
        if (_webDavBusy) return;
        PersistReadingSettings();

        if (!TryGetWebDavSettings(out var settings))
        {
            SetWebDavStatus(
                LocalText("请先配置 WebDAV。", "先に WebDAV を設定してください。", "Configure WebDAV first."),
                InfoBarSeverity.Warning);
            return;
        }

        SetWebDavBusy(true);
        try
        {
            var password = _webDavCredentialStore.Read(settings.Endpoint, settings.Username) ?? string.Empty;

            while (true)
            {
                BeginWebDavProgress(LocalText("正在读取云端存档列表…", "クラウド アーカイブ一覧を取得しています…", "Reading cloud archive list…"));
                var listing = await _webDavSyncService.ListArchivesAsync(settings, password);
                EndWebDavProgress();

                if (!listing.Succeeded)
                {
                    SetWebDavStatus(
                        LocalText("无法读取云端存档列表。", "クラウド アーカイブ一覧を取得できません。", "Could not read the cloud archive list."),
                        InfoBarSeverity.Error);
                    return;
                }

                if (listing.Items.Count == 0)
                {
                    SetWebDavStatus(
                        LocalText("云端暂无存档。", "クラウド アーカイブはまだありません。", "No cloud archives yet."),
                        InfoBarSeverity.Informational);
                    return;
                }

                // Exact dialog structure copied from UrbanPlanToolbox
                // WebDavDataManagementControl.OnManageCloudBackups @
                // 249bbf99088e5edc92b9a6f9b7635ca777cf847e.
                var list = CreateBackupList(listing.Items);
                var dialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = LocalText("WebDAV 云存档", "WebDAV クラウドアーカイブ", "WebDAV cloud archives"),
                    Content = list,
                    PrimaryButtonText = LocalText("恢复", "復元", "Restore"),
                    SecondaryButtonText = LocalText("删除", "削除", "Delete"),
                    CloseButtonText = LocalText("关闭", "閉じる", "Close"),
                    DefaultButton = ContentDialogButton.Close,
                    IsPrimaryButtonEnabled = false,
                    IsSecondaryButtonEnabled = false
                };
                list.SelectionChanged += (_, _) =>
                {
                    var hasSelection = list.SelectedItem is ListViewItem { Tag: WebDavArchiveItem };
                    dialog.IsPrimaryButtonEnabled = hasSelection;
                    dialog.IsSecondaryButtonEnabled = hasSelection;
                };

                var action = await dialog.ShowAsync();
                if (action == ContentDialogResult.None) return;
                if (list.SelectedItem is not ListViewItem { Tag: WebDavArchiveItem selected }) continue;

                if (action == ContentDialogResult.Primary)
                {
                    if (await RestoreBackupAsync(settings, password, selected)) return;
                    continue;
                }

                var deleteConfirm = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = LocalText("删除云存档？", "クラウドアーカイブを削除しますか？", "Delete cloud archive?"),
                    Content = string.Format(
                        LocalText("这将永久删除远端文件 {0}，无法撤销。", "この操作はリモートの {0} を削除します。元に戻せません。", "This permanently deletes {0} from the remote server."),
                        selected.FileName),
                    PrimaryButtonText = LocalText("删除", "削除", "Delete"),
                    CloseButtonText = LocalText("关闭", "閉じる", "Close"),
                    DefaultButton = ContentDialogButton.Close
                };
                if (await deleteConfirm.ShowAsync() != ContentDialogResult.Primary) continue;

                BeginWebDavProgress(LocalText("正在删除云存档…", "クラウド アーカイブを削除しています…", "Deleting cloud archive…"));
                await _webDavSyncService.DeleteArchiveAsync(settings, password, selected);
                EndWebDavProgress();
                SetWebDavStatus(
                    LocalText("云存档已删除。", "クラウド アーカイブを削除しました。", "Cloud archive deleted."),
                    InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("WebDAV archive management failed", ex);
            SetWebDavStatus(
                LocalText("云存档管理操作失败。", "クラウド アーカイブ管理に失敗しました。", "Cloud archive management failed."),
                InfoBarSeverity.Error);
        }
        finally
        {
            EndWebDavProgress();
            SetWebDavBusy(false);
        }
    }

    private async Task<WebDavArchiveItem?> SelectBackupAsync(
        IReadOnlyList<WebDavArchiveItem> items,
        string title,
        string primaryButtonText)
    {
        // Exact restore-picker structure copied from UrbanPlanToolbox
        // WebDavDataManagementControl.SelectBackupAsync @
        // 249bbf99088e5edc92b9a6f9b7635ca777cf847e.
        var list = CreateBackupList(items);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = list,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = LocalText("关闭", "閉じる", "Close"),
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false
        };
        list.SelectionChanged += (_, _) =>
            dialog.IsPrimaryButtonEnabled = list.SelectedItem is ListViewItem { Tag: WebDavArchiveItem };

        var action = await dialog.ShowAsync();
        return action == ContentDialogResult.Primary
               && list.SelectedItem is ListViewItem { Tag: WebDavArchiveItem selected }
            ? selected
            : null;
    }

    private async Task<bool> RestoreBackupAsync(
        WebDavConnectionSettings settings,
        string password,
        WebDavArchiveItem selected)
    {
        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = LocalText("从云存档恢复？", "クラウドアーカイブから復元しますか？", "Restore from cloud archive?"),
            Content = LocalText(
                "所选云存档会合并到本地；书本文件会恢复到 PageArc 的持久化书库目录，本地现有书本不会被删除。",
                "選択したクラウド アーカイブをローカルへマージします。書籍ファイルは PageArc の永続ライブラリに復元され、既存のローカル書籍は削除されません。",
                "The selected cloud archive will be merged locally. Book files are restored into PageArc's durable library and existing local books are not deleted."),
            PrimaryButtonText = LocalText("恢复", "復元", "Restore"),
            CloseButtonText = LocalText("关闭", "閉じる", "Close"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return false;

        var remotePath = Path.Combine(Path.GetTempPath(), $"PageArc-cloud-restore-{Guid.NewGuid():N}{ReadingBackupService.PackageExtension}");
        BeginWebDavProgress(
            string.Format(
                LocalText("正在恢复：{0}", "復元中：{0}", "Restoring: {0}"),
                selected.FileName));
        try
        {
            var downloaded = await _webDavSyncService.DownloadArchiveAsync(
                settings,
                password,
                selected,
                remotePath,
                CreateMappedTransferProgress(5, 62, LocalText("正在下载所选云存档…", "選択したクラウド アーカイブをダウンロードしています…", "Downloading selected cloud archive…")));
            if (!downloaded)
                throw new FileNotFoundException("The selected cloud archive no longer exists.", selected.FileName);

            SetWebDavProgress(68, LocalText("正在校验并恢复书本文件…", "検証して書籍ファイルを復元しています…", "Validating and restoring book files…"));
            var remote = ReadingBackupService.ReadPackage(remotePath);
            var restoredBookFiles = await _backupService.RestorePackageBooksAsync(remotePath, remote, App.Library);

            SetWebDavProgress(86, LocalText("正在恢复阅读数据…", "読書データを復元しています…", "Restoring reading data…"));
            var result = _backupService.Restore(remote, App.ReadingData, App.Library.Books, ReadingBackupRestoreMode.Merge);
            App.Library.Save();
            UpdateLocalBackupStatus();

            SetWebDavProgress(100, LocalText("恢复完成", "復元完了", "Restore complete"));
            SetWebDavStatus(
                LocalText(
                    $"云端恢复完成：接入 {restoredBookFiles} 个书本文件，恢复 {result.RestoredBookmarks} 个书签、{result.RestoredAnnotations} 条标注/笔记和 {result.RestoredProgress} 条阅读进度。",
                    $"クラウド復元完了：書籍ファイル {restoredBookFiles} 件、しおり {result.RestoredBookmarks} 件、注釈/ノート {result.RestoredAnnotations} 件、読書位置 {result.RestoredProgress} 件を復元しました。",
                    $"Cloud restore complete: {restoredBookFiles} book files, {result.RestoredBookmarks} bookmarks, {result.RestoredAnnotations} annotations/notes, and {result.RestoredProgress} reading positions restored."),
                InfoBarSeverity.Success);
            return true;
        }
        finally
        {
            TryDelete(remotePath);
            EndWebDavProgress();
        }
    }

    private ListView CreateBackupList(IEnumerable<WebDavArchiveItem> items)
    {
        // Exact measurements from UrbanPlanToolbox WebDavDataManagementControl.CreateBackupList.
        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MinWidth = 520,
            MaxHeight = 360
        };
        foreach (var item in items)
            list.Items.Add(new ListViewItem { Content = FormatBackupItem(item), Tag = item });
        return list;
    }

    private string FormatBackupItem(WebDavArchiveItem item)
    {
        // Exact line structure from UrbanPlanToolbox:
        // timestamp + version + size, then filename on the next line.
        var timestamp = item.SortTimeUtc == DateTimeOffset.MinValue
            ? "—"
            : item.SortTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        var version = string.IsNullOrWhiteSpace(item.AppVersion) ? "—" : $"v{item.AppVersion}";
        return $"{timestamp}   {version}   {FormatBytes(item.Size)}\n{item.FileName}";
    }

    private bool TryGetWebDavSettings(out WebDavConnectionSettings settings)
    {
        settings = new WebDavConnectionSettings(App.Settings.Current.WebDavEndpoint, App.Settings.Current.WebDavUsername);
        try
        {
            _ = settings.GetCollectionUri();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string CreateCloudArchiveFileName() =>
        WebDavArchiveItem.CreateFileName(DateTimeOffset.UtcNow, GetAppVersion());

    private static string GetAppVersion()
    {
        var version = typeof(SettingsPage).Assembly.GetName().Version;
        if (version is null) return "1.3.1";
        return version.Build >= 0
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : $"{version.Major}.{version.Minor}";
    }

    private void CompleteWebDavSync(string message)
    {
        SetWebDavProgress(100, LocalText("同步完成", "同期完了", "Sync complete"));
        App.Settings.Update(value => value.WebDavLastSyncAt = DateTimeOffset.Now);
        UpdateWebDavStatus();
        UpdateLocalBackupStatus();
        SetWebDavStatus(message, InfoBarSeverity.Success);
    }

    private void BeginWebDavProgress(string status)
    {
        WebDavSyncProgress.IsIndeterminate = false;
        WebDavSyncProgress.Value = 0;
        WebDavSyncProgress.Visibility = Visibility.Visible;
        WebDavStatusValue.Text = status;
    }

    private void SetWebDavProgress(double percent, string status)
    {
        WebDavSyncProgress.IsIndeterminate = false;
        WebDavSyncProgress.Value = Math.Clamp(percent, 0, 100);
        WebDavSyncProgress.Visibility = Visibility.Visible;
        WebDavStatusValue.Text = status;
    }

    private void EndWebDavProgress()
    {
        WebDavSyncProgress.IsIndeterminate = false;
        WebDavSyncProgress.Visibility = Visibility.Collapsed;
    }

    private IProgress<WebDavTransferProgress> CreateMappedTransferProgress(double start, double end, string status) =>
        new Progress<WebDavTransferProgress>(value =>
        {
            var fraction = value.TotalBytes is > 0 ? value.Fraction : 0;
            SetWebDavProgress(start + ((end - start) * fraction), status);
        });

    private static string FormatBytes(long bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / (1024d * 1024d):0.##} MB"
            : $"{bytes / 1024d:0.##} KB";

    private void UpdateWebDavStatus()
    {
        UpdateLocalBackupStatus();

        if (string.IsNullOrWhiteSpace(App.Settings.Current.WebDavEndpoint))
        {
            WebDavStatusValue.Text = LocalText("未配置", "未設定", "Not configured");
        }
        else if (App.Settings.Current.WebDavLastSyncAt is DateTimeOffset lastSync)
        {
            WebDavStatusValue.Text = string.Format(
                LocalText("已连接 · 上次同步 {0:yyyy-MM-dd HH:mm}", "接続済み · 最終同期 {0:yyyy-MM-dd HH:mm}", "Connected · last synced {0:yyyy-MM-dd HH:mm}"),
                lastSync.ToLocalTime());
        }
        else
        {
            WebDavStatusValue.Text = LocalText("已配置 · 尚未完成首次同步", "設定済み · 初回同期前", "Configured · first sync pending");
        }

        var configured = !string.IsNullOrWhiteSpace(App.Settings.Current.WebDavEndpoint);
        WebDavBackupButton.IsEnabled = !_webDavBusy && configured;
        WebDavRestoreButton.IsEnabled = !_webDavBusy && configured;
        WebDavManageButton.IsEnabled = !_webDavBusy && configured;
    }

    private void UpdateLocalBackupStatus()
    {
        var total = App.Library.Books.Count;
        var available = App.Library.Books.Count(book => !book.IsMissing && File.Exists(book.FilePath));
        LocalBackupStatus.Text = string.Format(
            LocalText("本地数据正常 · {0}/{1} 本书文件可用", "ローカル データ正常 · 書籍ファイル {0}/{1} 件利用可能", "Local data healthy · {0}/{1} book files available"),
            available,
            total);
    }

    private void SetWebDavBusy(bool busy)
    {
        _webDavBusy = busy;
        WebDavBackupButton.IsEnabled = !busy && !string.IsNullOrWhiteSpace(App.Settings.Current.WebDavEndpoint);
        WebDavRestoreButton.IsEnabled = !busy && !string.IsNullOrWhiteSpace(App.Settings.Current.WebDavEndpoint);
        WebDavManageButton.IsEnabled = !busy && !string.IsNullOrWhiteSpace(App.Settings.Current.WebDavEndpoint);
        WebDavConfigureButton.IsEnabled = !busy;
    }

    private void SetDataStatus(string message, InfoBarSeverity severity)
    {
        DataStatusBar.Severity = severity;
        DataStatusBar.Message = message;
        DataStatusBar.IsOpen = true;
    }

    private void SetWebDavStatus(string message, InfoBarSeverity severity)
    {
        WebDavStatusBar.Severity = severity;
        WebDavStatusBar.Message = message;
        WebDavStatusBar.IsOpen = true;
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
