using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;
using PageArc.Services;
using Windows.System;

namespace PageArc.Pages;

public sealed partial class AboutPage : Page
{
    private Uri? _releaseUri;

    public AboutPage()
    {
        InitializeComponent();
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var localizedTemplate = App.Localization.GetString("About_Version.Text");
        AboutVersionText.Text = Regex.Replace(localizedTemplate, @"\d+\.\d+\.\d+", version, RegexOptions.CultureInvariant);
        LicenseBodyText.Text = RuntimeText.Current(
            "内置：foliate-js（MIT）、fflate（MIT）。calibre 仅作为可选外部转换提供程序，不随 PageArc 分发。详见 THIRD_PARTY_NOTICES.md。",
            "同梱：foliate-js（MIT）、fflate（MIT）。calibre は任意の外部変換プロバイダーで、PageArc には同梱されません。詳細は THIRD_PARTY_NOTICES.md を参照してください。",
            "Bundled: foliate-js (MIT) and fflate (MIT). calibre is an optional external conversion provider and is not distributed with PageArc. See THIRD_PARTY_NOTICES.md.");
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateInfoBar.IsOpen = false;
        UpdateStatusText.Text = App.Localization.GetString("Update_Checking");
        try
        {
            var result = await App.Updates.CheckForUpdatesAsync();
            _releaseUri = result.ReleaseUri;
            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    UpdateStatusText.Text = string.Format(App.Localization.GetString("Update_Available"), result.RemoteVersion);
                    UpdateInfoBar.Severity = InfoBarSeverity.Success;
                    UpdateInfoBar.Title = App.Localization.GetString("Update_AvailableTitle");
                    UpdateInfoBar.Message = UpdateStatusText.Text;
                    UpdateInfoBar.ActionButton = CreateReleaseButton();
                    UpdateInfoBar.IsOpen = true;
                    break;
                case UpdateCheckStatus.UpToDate: UpdateStatusText.Text = App.Localization.GetString("Update_UpToDate"); break;
                case UpdateCheckStatus.NoRelease: UpdateStatusText.Text = App.Localization.GetString("Update_NoRelease"); break;
                case UpdateCheckStatus.RateLimited: UpdateStatusText.Text = App.Localization.GetString("Update_RateLimited"); break;
                default: UpdateStatusText.Text = App.Localization.GetString("Update_Failed"); break;
            }
        }
        finally { CheckUpdatesButton.IsEnabled = true; }
    }

    private Button? CreateReleaseButton()
    {
        if (_releaseUri is null) return null;
        var button = new Button { Content = App.Localization.GetString("Update_OpenRelease") };
        button.Click += async (_, _) => await Launcher.LaunchUriAsync(_releaseUri);
        return button;
    }
}
