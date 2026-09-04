using System.Xml.Linq;
using Xunit;

namespace PageArc.Tests;

public sealed class SettingsAboutUpdateDesignTests
{
    [Fact]
    public void SettingsAndAbout_UseFigmaSectionHierarchyAndResponsiveStates()
    {
        var root = FindRepoRoot();
        var settings = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml"));
        var about = File.ReadAllText(Path.Combine(root, "Pages", "AboutPage.xaml"));

        Assert.Contains("PageArcSectionStyle", settings, StringComparison.Ordinal);
        Assert.Contains("PageArcInsetStyle", settings, StringComparison.Ordinal);
        Assert.Contains("MinWindowWidth=\"1050\"", settings, StringComparison.Ordinal);
        Assert.Contains("ReadingTheme_App", settings, StringComparison.Ordinal);
        Assert.Contains("PageArcSectionStyle", about, StringComparison.Ordinal);
        Assert.Contains("UpdateAvailableVersionText", about, StringComparison.Ordinal);
        Assert.Contains("DownloadProgress", about, StringComparison.Ordinal);
        Assert.Contains("UpdateCheckProgressRing", about, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateActionIcon", about, StringComparison.Ordinal);
        Assert.Contains("WebDavStatusBar", settings, StringComparison.Ordinal);
        Assert.Contains("DataManagementResponsiveStates", settings, StringComparison.Ordinal);
        Assert.Contains("Width=\"40\" MinWidth=\"0\" Padding=\"0\"", settings, StringComparison.Ordinal);
        Assert.Contains("MinWindowWidth=\"1050\"", about, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsAndAbout_MirrorSpatialViewerNeutralFluentDoubleCards()
    {
        var root = FindRepoRoot();
        var resources = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var sectionStart = resources.IndexOf("x:Key=\"PageArcSectionStyle\"", StringComparison.Ordinal);
        var insetStart = resources.IndexOf("x:Key=\"PageArcInsetStyle\"", StringComparison.Ordinal);
        Assert.True(sectionStart >= 0 && insetStart > sectionStart);
        var section = resources[sectionStart..insetStart];
        var inset = resources[insetStart..];

        Assert.Contains("CardBackgroundFillColorDefaultBrush", section, StringComparison.Ordinal);
        Assert.Contains("CardStrokeColorDefaultBrush", section, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"1\"", section, StringComparison.Ordinal);
        Assert.Contains("ControlFillColorDefaultBrush", inset, StringComparison.Ordinal);
        Assert.Contains("ControlStrokeColorDefaultBrush", inset, StringComparison.Ordinal);
        Assert.Contains("BorderThickness\" Value=\"1\"", inset, StringComparison.Ordinal);
        Assert.DoesNotContain("PageArcSectionBrush", section, StringComparison.Ordinal);
        Assert.DoesNotContain("PageArcInsetBrush", inset, StringComparison.Ordinal);
    }

    [Fact]
    public void FollowAppReadingTheme_IsLocalizedInEverySupportedLanguage()
    {
        var root = FindRepoRoot();
        foreach (var language in new[] { "zh-CN", "ja-JP", "en-US" })
        {
            var document = XDocument.Load(Path.Combine(root, "Strings", language, "Resources.resw"));
            Assert.Contains(document.Descendants("data"), element =>
                string.Equals((string?)element.Attribute("name"), "ReadingTheme_App.Content", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(element.Element("value")?.Value));
        }
    }

    [Fact]
    public void UpdateSystem_SeparatesStoreAndVerifiedGitHubDeploymentChannels()
    {
        var root = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "PageArc.csproj"));
        var service = File.ReadAllText(Path.Combine(root, "Services", "GitHubUpdateService.cs"));
        var verifier = File.ReadAllText(Path.Combine(root, "Services", "MsixPackageSignatureVerifier.cs"));
        var about = File.ReadAllText(Path.Combine(root, "Pages", "AboutPage.xaml.cs"));
        var channel = File.ReadAllText(Path.Combine(root, "Services", "DistributionChannel.cs"));
        var githubManifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        var storeManifest = File.ReadAllText(Path.Combine(root, "Package.Store.appxmanifest"));

        Assert.Contains("PageArcDistributionChannel", project, StringComparison.Ordinal);
        Assert.Contains("PAGEARC_STORE", project, StringComparison.Ordinal);
        Assert.Contains("System.Security.Cryptography.Pkcs", project, StringComparison.Ordinal);
        Assert.Contains("DistributionChannel.IsStore", service, StringComparison.Ordinal);
        Assert.Contains(".msixbundle", service, StringComparison.Ordinal);
        Assert.Contains("Do not select .appinstaller", service, StringComparison.Ordinal);
        Assert.Contains("DownloadInstallerAsync", service, StringComparison.Ordinal);
        Assert.Contains("DownloadAndPrepareAsync", service, StringComparison.Ordinal);
        Assert.Contains("InstallPreparedUpdateAsync", service, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS.txt", service, StringComparison.Ordinal);
        Assert.Contains("SHA256.HashDataAsync", service, StringComparison.Ordinal);
        Assert.Contains("ExpectedSignerSubject = \"CN=AppPublisher\"", service, StringComparison.Ordinal);
        Assert.Contains("ExpectedSignerThumbprint = \"BD85AD77A651C86CA01A480C8E9BC64952993F98\"", service, StringComparison.Ordinal);
        Assert.Contains("_signatureVerifier.Verify", service, StringComparison.Ordinal);
        Assert.Contains("new PackageManager()", service, StringComparison.Ordinal);
        Assert.Contains("AddPackageAsync", service, StringComparison.Ordinal);
        Assert.Contains("DeploymentOptions.ForceApplicationShutdown", service, StringComparison.Ordinal);
        Assert.Contains("ApplicationRestartRegistration.Register", service, StringComparison.Ordinal);
        Assert.DoesNotContain("DeferRegistrationWhenPackagesAreInUse", service, StringComparison.Ordinal);
        Assert.DoesNotContain("AddPackageByUriAsync", service, StringComparison.Ordinal);
        Assert.Contains("WinVerifyTrust", verifier, StringComparison.Ordinal);
        Assert.Contains("AppxSignature.p7x", verifier, StringComparison.Ordinal);
        Assert.Contains("SignedCms", verifier, StringComparison.Ordinal);
        Assert.Contains("await App.Updates.DownloadAndPrepareAsync", about, StringComparison.Ordinal);
        Assert.Contains("await App.Updates.InstallPreparedUpdateAsync", about, StringComparison.Ordinal);
        Assert.Contains("RestartAfterCompletedDeployment", about, StringComparison.Ordinal);
        Assert.DoesNotContain("Launcher.LaunchUriAsync", about, StringComparison.Ordinal);
        Assert.DoesNotContain("Launcher.LaunchFileAsync", about, StringComparison.Ordinal);

        Assert.Contains("StoreContext.GetDefault", service, StringComparison.Ordinal);
        Assert.Contains("InitializeWithWindow.Initialize", service, StringComparison.Ordinal);
        Assert.Contains("RequestDownloadAndInstallStorePackageUpdatesAsync", service, StringComparison.Ordinal);
        Assert.Contains("packageManagement", githubManifest, StringComparison.Ordinal);
        Assert.DoesNotContain("packageManagement", storeManifest, StringComparison.Ordinal);
        Assert.Contains("Version=\"2026.904.140.0\"", storeManifest, StringComparison.Ordinal);
        Assert.DoesNotContain("prepare-calibre-runtime.ps1", File.ReadAllText(Path.Combine(root, ".github", "workflows", "store-release.yml")), StringComparison.Ordinal);
        Assert.Contains("PageArc.ConversionRuntime", File.ReadAllText(Path.Combine(root, "Services", "Conversion", "ConversionRuntimeManager.cs")), StringComparison.Ordinal);
        Assert.Contains("pagearc-store-publish", File.ReadAllText(Path.Combine(root, ".github", "workflows", "store-release.yml")), StringComparison.Ordinal);
        Assert.Contains("--inputDirectory", File.ReadAllText(Path.Combine(root, ".github", "workflows", "store-release.yml")), StringComparison.Ordinal);
        Assert.Contains("ConversionRuntimeDetached", File.ReadAllText(Path.Combine(root, "Packaging", "Build-StorePackage.ps1")), StringComparison.Ordinal);
        Assert.Contains("Microsoft Store", channel, StringComparison.Ordinal);
        Assert.Contains("GitHub Releases", channel, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsAndAbout_FillTheAvailableContentWidth()
    {
        var root = FindRepoRoot();
        var settings = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml"));
        var about = File.ReadAllText(Path.Combine(root, "Pages", "AboutPage.xaml"));
        Assert.DoesNotContain("MaxWidth=\"1320\"", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=\"1320\"", about, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateState_IsSessionScopedAndReleaseNotesAreLocaleAware()
    {
        var root = FindRepoRoot();
        var updater = File.ReadAllText(Path.Combine(root, "Services", "GitHubUpdateService.cs"));
        var about = File.ReadAllText(Path.Combine(root, "Pages", "AboutPage.xaml.cs"));
        var presentation = File.ReadAllText(Path.Combine(root, "Services", "ReleaseNotesPresentation.cs"));
        Assert.Contains("LastResult", updater, StringComparison.Ordinal);
        Assert.Contains("github-pending-update.json", updater, StringComparison.Ordinal);
        Assert.Contains("App.Updates.LastResult", about, StringComparison.Ordinal);
        Assert.Contains("IsGitHubUpdateReadyToInstall", about, StringComparison.Ordinal);
        Assert.Contains("zh-CN", presentation, StringComparison.Ordinal);
        Assert.Contains("ja-JP", presentation, StringComparison.Ordinal);
        Assert.Contains("en-US", presentation, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "PageArc.csproj"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("PageArc repository root not found.");
    }
}