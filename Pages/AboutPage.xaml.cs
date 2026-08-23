using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using PageArc.Services;
using Windows.ApplicationModel;
using Windows.System;

namespace PageArc.Pages;

public sealed partial class AboutPage : Page
{
    private UpdateCheckResult? _pendingUpdate;

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
        UpdateSectionHint.Text = LocalText("检查、下载并交由 Windows 安装更新", "更新を確認、ダウンロードし Windows でインストール", "Check, download, and hand updates to Windows Installer");
        CurrentVersionLabel.Text = LocalText("当前版本", "現在のバージョン", "Current version");
        AvailableVersionLabel.Text = LocalText("可用版本", "利用可能なバージョン", "Available version");
        UpdateStatusLabel.Text = LocalText("状态与发行说明", "状態とリリース ノート", "Status and release notes");
        UpdateSourceLabel.Text = LocalText("更新来源", "更新元", "Update source");
        UpdateSourceText.Text = DistributionChannel.Name;
        UpdateSourceStatusText.Text = DistributionChannel.IsStore
            ? LocalText("更新由 Microsoft Store 管理", "更新は Microsoft Store によって管理されます", "Updates are managed by Microsoft Store")
            : LocalText("仅使用 PageArc 官方 GitHub Release", "PageArc 公式 GitHub Release のみを使用", "Official PageArc GitHub Releases only");
        ProductInfoHeading.Text = LocalText("产品信息", "製品情報", "Product information");
        ProductInfoHint.Text = LocalText("格式、存储、隐私与第三方许可", "形式、保存、プライバシー、サードパーティ ライセンス", "Formats, storage, privacy, and third-party licensing");
        LicenseBodyText.Text = RuntimeText.Current(
            "内置：foliate-js（MIT）、fflate（MIT）；官方 x64 包同时内置 calibre 9.13.0 转换运行时（GPLv3）。详见 THIRD_PARTY_NOTICES.md。",
            "同梱：foliate-js（MIT）、fflate（MIT）。公式 x64 パッケージには calibre 9.13.0 変換ランタイム（GPLv3）も含まれます。詳細は THIRD_PARTY_NOTICES.md を参照してください。",
            "Bundled: foliate-js (MIT), fflate (MIT), and calibre 9.13.0 conversion runtime (GPLv3) in official x64 packages. See THIRD_PARTY_NOTICES.md.");

        if (DistributionChannel.IsStore)
        {
            UpdateStatusText.Text = LocalText("更新由 Microsoft Store 提供。", "更新は Microsoft Store から提供されます。", "Updates are provided by Microsoft Store.");
            UpdateActionText.Text = LocalText("打开 Microsoft Store", "Microsoft Store を開く", "Open Microsoft Store");
        }
        else if (App.Updates.LastResult is { } priorResult)
        {
            ApplyUpdateResult(priorResult, showNotification: false);
        }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (DistributionChannel.IsStore)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-windows-store://search/?query=PageArc"));
            return;
        }
        if (_pendingUpdate is { Status: UpdateCheckStatus.UpdateAvailable } pending)
        {
            await DownloadOrOpenUpdateAsync(pending);
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
        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable:
                UpdateStatusText.Text = string.Format(App.Localization.GetString("Update_Available"), result.RemoteVersion);
                UpdateInfoBar.Severity = InfoBarSeverity.Success;
                UpdateInfoBar.Title = App.Localization.GetString("Update_AvailableTitle");
                UpdateInfoBar.Message = result.InstallerUri is null
                    ? LocalText("此版本没有兼容的安装资产，将打开发布页。", "互換インストーラーがないためリリース ページを開きます。", "No compatible installer asset was found; the release page will open.")
                    : LocalText($"已找到 {result.InstallerName}", $"{result.InstallerName} が見つかりました", $"Found {result.InstallerName}");
                UpdateInfoBar.IsOpen = showNotification;
                UpdateActionText.Text = result.InstallerUri is null
                    ? App.Localization.GetString("Update_OpenRelease")
                    : LocalText("下载并安装", "ダウンロードしてインストール", "Download and install");
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

    private void ResetUpdateAction()
    {
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

    private async Task DownloadOrOpenUpdateAsync(UpdateCheckResult update)
    {
        if (update.InstallerUri is null)
        {
            if (update.ReleaseUri is not null) await Launcher.LaunchUriAsync(update.ReleaseUri);
            return;
        }

        CheckUpdatesButton.IsEnabled = false;
        DownloadProgress.Visibility = Visibility.Visible;
        DownloadProgress.Value = 0;
        UpdateStatusText.Text = LocalText("正在下载安装包…", "インストーラーをダウンロードしています…", "Downloading installer…");
        try
        {
            var progress = new Progress<double>(value => DownloadProgress.Value = value);
            var file = await App.Updates.DownloadInstallerAsync(update, progress);
            UpdateStatusText.Text = LocalText("下载完成，正在打开 Windows 安装器。", "ダウンロード完了。Windows インストーラーを開きます。", "Download complete. Opening Windows Installer.");
            await Launcher.LaunchFileAsync(file);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Update installer download failed", ex);
            UpdateStatusText.Text = App.Localization.GetString("Update_Failed");
            if (update.ReleaseUri is not null) await Launcher.LaunchUriAsync(update.ReleaseUri);
        }
        finally
        {
            DownloadProgress.Visibility = Visibility.Collapsed;
            CheckUpdatesButton.IsEnabled = true;
        }
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
