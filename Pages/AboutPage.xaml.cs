using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using PageArc.Services;
using PageArc.Services.Conversion;
using Windows.ApplicationModel;

namespace PageArc.Pages;

public sealed partial class AboutPage : Page
{
    private UpdateCheckResult? _pendingUpdate;
    private bool _restartRequired;
    private readonly ConversionRuntimeManager _runtimeManager = new();
    private ConversionRuntimeRelease? _pendingRuntimeRelease;
    private bool _runtimeBusy;

    public AboutPage()
    {
        InitializeComponent();
        var version = typeof(App).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
        var displayVersion = version.Build > 0
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : $"{version.Major}.{version.Minor}";
        AboutVersionText.Text = $"v{displayVersion}";
        UpdateLocalVersionText.Text = $"v{displayVersion}";
        UpdateAvailableVersionText.Text = "—";
        AboutChannelText.Text = DistributionChannel.Name;
        PackageVersionText.Text = GetPackageVersion(version);
        PublisherText.Text = LocalText("发布者 · Jo Kiyō", "発行元 · Jo Kiyō", "Publisher · Jo Kiyō");
        ArchitectureText.Text = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        StackText.Text = "C# · WinUI 3 · Windows App SDK · MSIX";
        ProductTaglineText.Text = LocalText("为 Windows 打造的流式电子书阅读器", "Windows 向けリフロー型電子書籍リーダー", "A reflow-first ebook reader for Windows");
        DisplayVersionLabel.Text = LocalText("显示版本与通道", "表示バージョンとチャネル", "Display version and channel");
        PackageVersionLabel.Text = LocalText("软件包版本与发布者", "パッケージ バージョンと発行元", "Package version and publisher");
        ArchitectureLabel.Text = LocalText("体系结构与技术栈", "アーキテクチャと技術スタック", "Architecture and stack");
        UpdateSectionHint.Text = LocalText("在 PageArc 内检查、下载并安装更新", "PageArc 内で更新を確認、ダウンロード、インストール", "Check, download, and install updates inside PageArc");
        CurrentVersionLabel.Text = LocalText("当前版本", "現在のバージョン", "Current version");
        AvailableVersionLabel.Text = LocalText("可用版本", "利用可能なバージョン", "Available version");
        UpdateStatusLabel.Text = LocalText("状态与发行说明", "状態とリリース ノート", "Status and release notes");
        UpdateSourceLabel.Text = LocalText("更新来源", "更新元", "Update source");
        UpdateSourceText.Text = DistributionChannel.Name;
        UpdateSourceStatusText.Text = DistributionChannel.IsStore
            ? LocalText("更新由 Microsoft Store 管理", "更新は Microsoft Store によって管理されます", "Updates are managed by Microsoft Store")
            : LocalText("仅使用 PageArc 官方 GitHub Release", "PageArc 公式 GitHub Release のみを使用", "Official PageArc GitHub Releases only");
        ExtensionManagementHeading.Text = LocalText("扩展更新管理", "拡張機能の更新管理", "Extension update management");
        ExtensionManagementHint.Text = LocalText(
            "管理可选运行时的下载安装与独立更新；这些扩展不包含在基础 PageArc 安装包中。",
            "オプション ランタイムのダウンロード、インストール、独立更新を管理します。これらは基本 PageArc パッケージには含まれません。",
            "Manage downloads, installation, and independent updates for optional runtimes that are not included in the base PageArc package.");
        ConversionRuntimeNameText.Text = "PageArc Conversion Runtime";
        ConversionRuntimeDescriptionText.Text = LocalText(
            "calibre 转换运行时，用于格式转换以及部分 MOBI / AZW3 / LIT 兼容路径。",
            "calibre 変換ランタイム。形式変換と一部の MOBI / AZW3 / LIT 互換処理に使用します。",
            "calibre conversion runtime for format conversion and selected MOBI / AZW3 / LIT compatibility paths.");
        RuntimeInstalledLabel.Text = LocalText("已安装版本", "インストール済み", "Installed version");
        RuntimeAvailableLabel.Text = LocalText("可用版本", "利用可能なバージョン", "Available version");
        RuntimeStorageLabel.Text = LocalText("本地占用", "ローカル使用量", "Local storage");
        CheckRuntimeUpdatesButton.Content = LocalText("检查扩展更新", "拡張機能の更新を確認", "Check extension updates");
        RemoveRuntimeButton.Content = LocalText("卸载运行时", "ランタイムを削除", "Remove runtime");
        RuntimeSourceText.Text = LocalText(
            "来源：KiYouJyo/PageArc.ConversionRuntime · 下载后执行 SHA-256 校验与可执行文件验证。",
            "提供元：KiYouJyo/PageArc.ConversionRuntime · ダウンロード後に SHA-256 と実行ファイルを検証します。",
            "Source: KiYouJyo/PageArc.ConversionRuntime · downloads are verified by SHA-256 and executable validation.");
        RefreshRuntimeCard();

        ProductInfoHeading.Text = LocalText("产品信息", "製品情報", "Product information");
        ProductInfoHint.Text = LocalText("格式、存储、隐私与第三方许可", "形式、保存、プライバシー、サードパーティ ライセンス", "Formats, storage, privacy, and third-party licensing");
        LicenseBodyText.Text = RuntimeText.Current(
            "内置：foliate-js（MIT）、fflate（MIT）。calibre 转换运行时已从基础安装包剥离，仅按需从 PageArc.ConversionRuntime 下载，并继续遵循 GPLv3。详见 THIRD_PARTY_NOTICES.md。",
            "同梱：foliate-js（MIT）、fflate（MIT）。calibre 変換ランタイムは基本パッケージから分離され、PageArc.ConversionRuntime から必要時のみ取得し、GPLv3 を継続して適用します。詳細は THIRD_PARTY_NOTICES.md を参照してください。",
            "Bundled: foliate-js (MIT) and fflate (MIT). The calibre conversion runtime is detached from the base package, downloaded on demand from PageArc.ConversionRuntime, and remains GPLv3-licensed. See THIRD_PARTY_NOTICES.md.");

        if (DistributionChannel.IsStore)
        {
            UpdateStatusText.Text = LocalText("由 Microsoft Store 提供更新，可直接在 PageArc 内安装。", "Microsoft Store の更新を PageArc 内で直接インストールできます。", "Microsoft Store updates can be installed directly inside PageArc.");
            ResetUpdateAction();
        }
        else if (App.Updates.LastResult is { } priorResult)
        {
            ApplyUpdateResult(priorResult, showNotification: false);
        }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (_restartRequired)
        {
            if (!DistributionChannel.IsStore && App.Updates.IsGitHubUpdateReadyToInstall)
            {
                await InstallPreparedGitHubUpdateAsync();
                return;
            }

            RestartAfterCompletedDeployment();
            return;
        }

        if (_pendingUpdate is { Status: UpdateCheckStatus.UpdateAvailable } pending)
        {
            await DownloadAndInstallUpdateAsync(pending);
            return;
        }

        SetUpdateCheckBusy(true);
        UpdateInfoBar.IsOpen = false;
        ReleaseNotesText.Text = string.Empty;
        UpdateStatusText.Text = App.Localization.GetString("Update_Checking");
        try
        {
            ApplyUpdateResult(await App.Updates.CheckForUpdatesAsync(), showNotification: true);
        }
        finally
        {
            SetUpdateCheckBusy(false);
        }
    }

    private void ApplyUpdateResult(UpdateCheckResult result, bool showNotification)
    {
        _pendingUpdate = result.Status == UpdateCheckStatus.UpdateAvailable ? result : null;
        UpdateAvailableVersionText.Text = result.Status == UpdateCheckStatus.UpdateAvailable && result.RemoteVersion is not null
            ? $"v{result.RemoteVersion.Major}.{result.RemoteVersion.Minor}.{result.RemoteVersion.Build}"
            : "—";
        ReleaseNotesText.Text = ReleaseNotesPresentation.ForLanguage(result.ReleaseNotes, App.Localization.CurrentLanguage);

        if (!DistributionChannel.IsStore &&
            result.Status == UpdateCheckStatus.UpdateAvailable &&
            App.Updates.IsGitHubUpdateReadyToInstall)
        {
            ShowPreparedGitHubUpdateState();
            return;
        }

        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable:
                UpdateStatusText.Text = string.Format(App.Localization.GetString("Update_Available"), result.RemoteVersion);
                UpdateInfoBar.Severity = InfoBarSeverity.Success;
                UpdateInfoBar.Title = App.Localization.GetString("Update_AvailableTitle");
                UpdateInfoBar.Message = DistributionChannel.IsStore
                    ? LocalText("Microsoft Store 更新已就绪", "Microsoft Store の更新を利用できます", "A Microsoft Store update is ready")
                    : result.InstallerUri is null
                    ? LocalText("此版本没有可在应用内安装的 MSIX 包。", "このバージョンにはアプリ内インストール対応の MSIX パッケージがありません。", "This release has no MSIX package supported for in-app installation.")
                    : result.ChecksumUri is null
                    ? LocalText("此版本缺少 SHA256SUMS.txt，已阻止应用内更新。", "SHA256SUMS.txt がないためアプリ内更新を停止しました。", "SHA256SUMS.txt is missing, so in-app update was blocked.")
                    : LocalText($"已找到 {result.InstallerName}", $"{result.InstallerName} が見つかりました", $"Found {result.InstallerName}");
                UpdateInfoBar.IsOpen = showNotification;
                UpdateActionText.Text = DistributionChannel.IsStore
                    ? LocalText("下载并安装", "ダウンロードしてインストール", "Download and install")
                    : result.InstallerUri is not null && result.ChecksumUri is not null
                        ? LocalText("下载并验证", "ダウンロードして検証", "Download and verify")
                        : App.Localization.GetString("About_CheckUpdatesText.Text");
                break;
            case UpdateCheckStatus.UpToDate:
                UpdateStatusText.Text = App.Localization.GetString("Update_UpToDate");
                ResetUpdateAction();
                break;
            case UpdateCheckStatus.NoRelease:
                UpdateStatusText.Text = App.Localization.GetString("Update_NoRelease");
                ResetUpdateAction();
                break;
            case UpdateCheckStatus.RateLimited:
                UpdateStatusText.Text = App.Localization.GetString("Update_RateLimited");
                ResetUpdateAction();
                break;
            default:
                UpdateStatusText.Text = App.Localization.GetString("Update_Failed");
                ResetUpdateAction();
                break;
        }
    }

    private void ShowPreparedGitHubUpdateState()
    {
        _restartRequired = true;
        _pendingUpdate = null;
        UpdateStatusText.Text = LocalText(
            "更新包已完成 SHA-256 与签名验证，点击“重启并更新”后由 Windows 安装并重新启动 PageArc。",
            "更新パッケージの SHA-256 と署名を検証済みです。「再起動して更新」を押すと Windows がインストールして PageArc を再起動します。",
            "The update package passed SHA-256 and signature verification. Restart to update lets Windows install it and relaunch PageArc.");
        UpdateActionText.Text = LocalText("重启并更新", "再起動して更新", "Restart to update");
    }

    private void ResetUpdateAction()
    {
        _restartRequired = false;
        UpdateActionText.Text = App.Localization.GetString("About_CheckUpdatesText.Text");
        UpdateInfoBar.IsOpen = false;
    }

    private void SetUpdateCheckBusy(bool busy)
    {
        CheckUpdatesButton.IsEnabled = !busy;
        UpdateActionText.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        UpdateCheckProgressRing.IsActive = busy;
        UpdateCheckProgressRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task DownloadAndInstallUpdateAsync(UpdateCheckResult update)
    {
        if (!DistributionChannel.IsStore && (update.InstallerUri is null || update.ChecksumUri is null))
        {
            UpdateStatusText.Text = LocalText(
                "此版本缺少可验证的 MSIX 包或 SHA256SUMS.txt，无法在应用内更新。",
                "検証可能な MSIX パッケージまたは SHA256SUMS.txt がないため、アプリ内で更新できません。",
                "This release is missing a verifiable MSIX package or SHA256SUMS.txt and cannot be updated in-app.");
            return;
        }

        CheckUpdatesButton.IsEnabled = false;
        DownloadProgress.Visibility = Visibility.Visible;
        DownloadProgress.Value = 0;
        UpdateStatusText.Text = DistributionChannel.IsStore
            ? LocalText("正在下载并安装更新…", "更新をダウンロードしてインストールしています…", "Downloading and installing the update…")
            : LocalText("正在下载并验证更新…", "更新をダウンロードして検証しています…", "Downloading and verifying the update…");
        try
        {
            var progress = new Progress<double>(value => DownloadProgress.Value = value);
            var result = DistributionChannel.IsStore
                ? await App.Updates.DownloadAndInstallAsync(update, progress)
                : await App.Updates.DownloadAndPrepareAsync(update, progress);
            switch (result.Status)
            {
                case UpdateInstallStatus.Completed when DistributionChannel.IsStore:
                    _restartRequired = true;
                    _pendingUpdate = null;
                    UpdateStatusText.Text = LocalText("Microsoft Store 更新已安装，重启 PageArc 后生效。", "Microsoft Store の更新をインストールしました。PageArc の再起動後に反映されます。", "The Microsoft Store update is installed and will take effect after PageArc restarts.");
                    UpdateActionText.Text = LocalText("重启 PageArc", "PageArc を再起動", "Restart PageArc");
                    break;
                case UpdateInstallStatus.RestartRequired when !DistributionChannel.IsStore:
                    ShowPreparedGitHubUpdateState();
                    break;
                case UpdateInstallStatus.Canceled:
                    UpdateStatusText.Text = LocalText("更新已取消。", "更新をキャンセルしました。", "The update was canceled.");
                    break;
                default:
                    StartupDiagnostics.Log($"In-app update failed: {result.ErrorMessage}");
                    UpdateStatusText.Text = string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? App.Localization.GetString("Update_Failed")
                        : LocalText($"更新失败：{result.ErrorMessage}", $"更新に失敗しました：{result.ErrorMessage}", $"Update failed: {result.ErrorMessage}");
                    break;
            }
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("In-app update failed", ex);
            UpdateStatusText.Text = App.Localization.GetString("Update_Failed");
        }
        finally
        {
            DownloadProgress.Visibility = Visibility.Collapsed;
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private async Task InstallPreparedGitHubUpdateAsync()
    {
        CheckUpdatesButton.IsEnabled = false;
        DownloadProgress.Visibility = Visibility.Visible;
        DownloadProgress.Value = 0;
        UpdateStatusText.Text = LocalText(
            "正在将已验证更新交给 Windows 安装。PageArc 将自动关闭并重新启动…",
            "検証済みの更新を Windows に渡してインストールしています。PageArc は自動的に終了して再起動します…",
            "Installing the verified update through Windows. PageArc will close and relaunch automatically…");
        try
        {
            var progress = new Progress<double>(value => DownloadProgress.Value = value);
            var result = await App.Updates.InstallPreparedUpdateAsync(progress);
            if (result.Status == UpdateInstallStatus.Completed)
            {
                // ForceApplicationShutdown normally terminates this process before the
                // deployment await returns. If Windows completed registration without
                // terminating us, a normal AppInstance restart is now safe because the
                // new package is already fully registered (unlike the old deferred flow).
                RestartAfterCompletedDeployment();
                return;
            }

            if (result.Status == UpdateInstallStatus.Canceled)
            {
                UpdateStatusText.Text = LocalText("更新已取消。", "更新をキャンセルしました。", "The update was canceled.");
                return;
            }

            StartupDiagnostics.Log($"Verified package deployment failed: {result.ErrorMessage}");
            UpdateStatusText.Text = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? App.Localization.GetString("Update_Failed")
                : LocalText($"Windows 安装更新失败：{result.ErrorMessage}", $"Windows による更新のインストールに失敗しました：{result.ErrorMessage}", $"Windows failed to install the update: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Verified package deployment failed", ex);
            UpdateStatusText.Text = App.Localization.GetString("Update_Failed");
        }
        finally
        {
            DownloadProgress.Visibility = Visibility.Collapsed;
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private void RestartAfterCompletedDeployment()
    {
        var failureReason = Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
        UpdateStatusText.Text = LocalText(
            $"更新已完成，但自动重启失败（{failureReason}）。请关闭并重新打开 PageArc。",
            $"更新は完了しましたが、自動再起動に失敗しました（{failureReason}）。PageArc を閉じて再度開いてください。",
            $"The update completed, but automatic restart failed ({failureReason}). Close and reopen PageArc.");
    }

    private async void CheckRuntimeUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (_runtimeBusy) return;
        SetRuntimeBusy(true);
        RuntimeInfoBar.IsOpen = false;
        RuntimeAvailableVersionText.Text = LocalText("检查中…", "確認中…", "Checking…");
        try
        {
            var result = await _runtimeManager.CheckForUpdatesAsync();
            if (!result.Succeeded)
            {
                _pendingRuntimeRelease = null;
                RuntimeAvailableVersionText.Text = "—";
                RuntimeInfoBar.Severity = InfoBarSeverity.Error;
                RuntimeInfoBar.Message = LocalText(
                    "无法检查扩展更新，请检查网络后重试。",
                    "拡張機能の更新を確認できません。ネットワークを確認して再試行してください。",
                    "Could not check extension updates. Check the network and try again.");
                RuntimeInfoBar.IsOpen = true;
                RefreshRuntimeCard();
                return;
            }

            _pendingRuntimeRelease = result.UpdateAvailable ? result.LatestCompatibleRelease : null;
            RuntimeAvailableVersionText.Text = result.LatestCompatibleRelease is null
                ? "—"
                : result.LatestCompatibleRelease.Manifest.PackageVersion;

            if (!result.LocalStatus.IsInstalled && result.LatestCompatibleRelease is not null)
            {
                RuntimeInfoBar.Severity = InfoBarSeverity.Informational;
                RuntimeInfoBar.Message = LocalText(
                    $"可安装转换运行时 {result.LatestCompatibleRelease.Manifest.PackageVersion}。",
                    $"変換ランタイム {result.LatestCompatibleRelease.Manifest.PackageVersion} をインストールできます。",
                    $"Conversion runtime {result.LatestCompatibleRelease.Manifest.PackageVersion} is available to install.");
                RuntimeInfoBar.IsOpen = true;
            }
            else if (result.UpdateAvailable && result.LatestCompatibleRelease is not null)
            {
                RuntimeInfoBar.Severity = InfoBarSeverity.Success;
                RuntimeInfoBar.Message = LocalText(
                    $"发现扩展更新：{result.LocalStatus.PackageVersion} → {result.LatestCompatibleRelease.Manifest.PackageVersion}。",
                    $"拡張機能の更新があります：{result.LocalStatus.PackageVersion} → {result.LatestCompatibleRelease.Manifest.PackageVersion}。",
                    $"Extension update available: {result.LocalStatus.PackageVersion} → {result.LatestCompatibleRelease.Manifest.PackageVersion}.");
                RuntimeInfoBar.IsOpen = true;
            }
            else
            {
                RuntimeInfoBar.Severity = InfoBarSeverity.Success;
                RuntimeInfoBar.Message = LocalText(
                    "转换运行时已是最新兼容版本。",
                    "変換ランタイムは最新の互換バージョンです。",
                    "The conversion runtime is already on the latest compatible version.");
                RuntimeInfoBar.IsOpen = true;
            }

            RefreshRuntimeCard(preserveAvailableVersion: true);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Runtime update management check failed", ex);
            RuntimeInfoBar.Severity = InfoBarSeverity.Error;
            RuntimeInfoBar.Message = LocalText(
                "检查扩展更新失败。",
                "拡張機能の更新確認に失敗しました。",
                "Extension update check failed.");
            RuntimeInfoBar.IsOpen = true;
            RefreshRuntimeCard();
        }
        finally
        {
            SetRuntimeBusy(false);
        }
    }

    private async void RuntimeAction_Click(object sender, RoutedEventArgs e)
    {
        if (_runtimeBusy) return;

        SetRuntimeBusy(true);
        RuntimeInfoBar.IsOpen = false;
        RuntimeDownloadProgress.Value = 0;
        RuntimeDownloadProgress.Visibility = Visibility.Visible;
        RuntimeProgressText.Visibility = Visibility.Visible;

        try
        {
            var progress = new Progress<ConversionRuntimeProgress>(value =>
            {
                var percent = value.TotalBytes is > 0 ? value.Fraction * 100 : 0;
                RuntimeDownloadProgress.Value = percent;
                RuntimeProgressText.Text = value.Stage switch
                {
                    "manifest" => LocalText("正在检查扩展清单…", "拡張機能のマニフェストを確認しています…", "Checking extension manifest…"),
                    "download" => LocalText(
                        $"正在下载转换运行时… {percent:0}% · {FormatRuntimeBytes(value.BytesTransferred)} / {FormatRuntimeBytes(value.TotalBytes ?? 0)}",
                        $"変換ランタイムをダウンロードしています… {percent:0}% · {FormatRuntimeBytes(value.BytesTransferred)} / {FormatRuntimeBytes(value.TotalBytes ?? 0)}",
                        $"Downloading conversion runtime… {percent:0}% · {FormatRuntimeBytes(value.BytesTransferred)} / {FormatRuntimeBytes(value.TotalBytes ?? 0)}"),
                    "extract" => LocalText("正在校验并安装…", "検証してインストールしています…", "Verifying and installing…"),
                    "complete" => LocalText("安装完成。", "インストール完了。", "Installation complete."),
                    _ => LocalText("正在准备扩展…", "拡張機能を準備しています…", "Preparing extension…")
                };
            });

            if (_pendingRuntimeRelease is not null)
                await _runtimeManager.InstallReleaseAsync(_pendingRuntimeRelease, progress);
            else
                await _runtimeManager.EnsureInstalledAsync(progress);

            _pendingRuntimeRelease = null;
            RuntimeInfoBar.Severity = InfoBarSeverity.Success;
            RuntimeInfoBar.Message = LocalText(
                "转换运行时安装/更新完成，可立即使用，无需重启 PageArc。",
                "変換ランタイムのインストール/更新が完了しました。PageArc の再起動は不要です。",
                "Conversion runtime installation/update completed and is ready immediately; no PageArc restart is required.");
            RuntimeInfoBar.IsOpen = true;
            RefreshRuntimeCard();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Runtime install/update failed", ex);
            RuntimeInfoBar.Severity = InfoBarSeverity.Error;
            RuntimeInfoBar.Message = LocalText(
                "转换运行时下载安装或校验失败。",
                "変換ランタイムのダウンロード、インストール、または検証に失敗しました。",
                "The conversion runtime download, installation, or verification failed.");
            RuntimeInfoBar.IsOpen = true;
            RefreshRuntimeCard();
        }
        finally
        {
            RuntimeDownloadProgress.Visibility = Visibility.Collapsed;
            RuntimeProgressText.Visibility = Visibility.Collapsed;
            SetRuntimeBusy(false);
        }
    }

    private async void RemoveRuntime_Click(object sender, RoutedEventArgs e)
    {
        if (_runtimeBusy || !_runtimeManager.IsInstalled) return;

        var confirmation = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = LocalText("卸载转换运行时？", "変換ランタイムを削除しますか？", "Remove conversion runtime?"),
            Content = LocalText(
                "这只会删除按需下载的转换运行时，不会删除书本、阅读数据或 PageArc 本体。之后需要时可重新下载。",
                "必要時にダウンロードした変換ランタイムだけを削除します。書籍、読書データ、PageArc 本体は削除されません。必要になれば再ダウンロードできます。",
                "This removes only the on-demand conversion runtime. Books, reading data, and PageArc remain untouched, and the runtime can be downloaded again later."),
            PrimaryButtonText = LocalText("卸载", "削除", "Remove"),
            CloseButtonText = LocalText("取消", "キャンセル", "Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;

        SetRuntimeBusy(true);
        try
        {
            _runtimeManager.RemoveInstalledRuntime();
            _pendingRuntimeRelease = null;
            RuntimeInfoBar.Severity = InfoBarSeverity.Success;
            RuntimeInfoBar.Message = LocalText(
                "转换运行时已卸载。",
                "変換ランタイムを削除しました。",
                "Conversion runtime removed.");
            RuntimeInfoBar.IsOpen = true;
            RefreshRuntimeCard();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Runtime removal failed", ex);
            RuntimeInfoBar.Severity = InfoBarSeverity.Error;
            RuntimeInfoBar.Message = LocalText(
                "转换运行时卸载失败，部分文件可能正在被使用。",
                "変換ランタイムの削除に失敗しました。一部のファイルが使用中の可能性があります。",
                "Could not remove the conversion runtime; some files may still be in use.");
            RuntimeInfoBar.IsOpen = true;
        }
        finally
        {
            SetRuntimeBusy(false);
        }
    }

    private void RefreshRuntimeCard(bool preserveAvailableVersion = false)
    {
        var status = _runtimeManager.GetStatus();
        ConversionRuntimeStateBadge.Text = status.IsInstalled
            ? LocalText("已安装", "インストール済み", "Installed")
            : LocalText("未安装", "未インストール", "Not installed");
        RuntimeInstalledVersionText.Text = status.IsInstalled ? status.PackageVersion : "—";
        if (!preserveAvailableVersion)
            RuntimeAvailableVersionText.Text = "—";
        RuntimeStorageText.Text = status.IsInstalled ? FormatRuntimeBytes(status.InstalledBytes) : "0 MB";

        RemoveRuntimeButton.IsEnabled = !_runtimeBusy && status.IsInstalled;
        CheckRuntimeUpdatesButton.IsEnabled = !_runtimeBusy && status.IsSupported;

        if (!status.IsSupported)
        {
            RuntimeActionButton.Content = LocalText("当前设备不支持", "このデバイスは未対応", "Unsupported device");
            RuntimeActionButton.IsEnabled = false;
        }
        else if (_pendingRuntimeRelease is not null)
        {
            RuntimeActionButton.Content = status.IsInstalled
                ? LocalText("下载并更新", "ダウンロードして更新", "Download and update")
                : LocalText("下载并安装", "ダウンロードしてインストール", "Download and install");
            RuntimeActionButton.IsEnabled = !_runtimeBusy;
        }
        else if (!status.IsInstalled)
        {
            RuntimeActionButton.Content = LocalText("下载并安装", "ダウンロードしてインストール", "Download and install");
            RuntimeActionButton.IsEnabled = !_runtimeBusy;
        }
        else
        {
            RuntimeActionButton.Content = LocalText("已安装", "インストール済み", "Installed");
            RuntimeActionButton.IsEnabled = false;
        }
    }

    private void SetRuntimeBusy(bool busy)
    {
        _runtimeBusy = busy;
        CheckRuntimeUpdatesButton.IsEnabled = !busy && _runtimeManager.IsSupported;
        RemoveRuntimeButton.IsEnabled = !busy && _runtimeManager.IsInstalled;
        if (busy)
            RuntimeActionButton.IsEnabled = false;
        else
            RefreshRuntimeCard(preserveAvailableVersion: _pendingRuntimeRelease is not null || RuntimeAvailableVersionText.Text != "—");
    }

    private static string FormatRuntimeBytes(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        if (bytes >= 1024L * 1024L * 1024L) return $"{bytes / (1024d * 1024d * 1024d):0.##} GB";
        return $"{bytes / (1024d * 1024d):0.##} MB";
    }

    private static string GetPackageVersion(Version fallback)
    {
        try
        {
            var value = Package.Current.Id.Version;
            return $"{value.Major}.{value.Minor}.{value.Build}.{value.Revision}";
        }
        catch
        {
            return fallback.ToString(4);
        }
    }

    private static string LocalText(string zh, string ja, string en)
    {
        var language = App.Localization.CurrentLanguage;
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return zh;
        if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return ja;
        return en;
    }
}