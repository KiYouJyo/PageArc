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
        Assert.Contains("WebDavProgressRing", settings, StringComparison.Ordinal);
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
    public void UpdateSystem_SeparatesGitHubAndStoreChannelsAndDownloadsInstallerAssetsOnly()
    {
        var root = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "PageArc.csproj"));
        var service = File.ReadAllText(Path.Combine(root, "Services", "GitHubUpdateService.cs"));
        var channel = File.ReadAllText(Path.Combine(root, "Services", "DistributionChannel.cs"));
        var githubManifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        var storeManifest = File.ReadAllText(Path.Combine(root, "Package.Store.appxmanifest"));

        Assert.Contains("PageArcDistributionChannel", project, StringComparison.Ordinal);
        Assert.Contains("PAGEARC_STORE", project, StringComparison.Ordinal);
        Assert.Contains("DistributionChannel.IsStore", service, StringComparison.Ordinal);
        Assert.Contains(".msixbundle", service, StringComparison.Ordinal);
        Assert.Contains("Do not select .appinstaller", service, StringComparison.Ordinal);
        Assert.Contains("DownloadInstallerAsync", service, StringComparison.Ordinal);
        Assert.Contains("StoreContext.GetDefault", service, StringComparison.Ordinal);
        Assert.Contains("InitializeWithWindow.Initialize", service, StringComparison.Ordinal);
        Assert.Contains("RequestDownloadAndInstallStorePackageUpdatesAsync", service, StringComparison.Ordinal);
        Assert.Contains("AddPackageByUriAsync", service, StringComparison.Ordinal);
        Assert.Contains("DeferRegistrationWhenPackagesAreInUse", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Launcher.LaunchUriAsync", File.ReadAllText(Path.Combine(root, "Pages", "AboutPage.xaml.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("Launcher.LaunchFileAsync", File.ReadAllText(Path.Combine(root, "Pages", "AboutPage.xaml.cs")), StringComparison.Ordinal);
        Assert.Contains("packageManagement", githubManifest, StringComparison.Ordinal);
        Assert.DoesNotContain("packageManagement", storeManifest, StringComparison.Ordinal);
        Assert.Contains("Version=\"2026.903.120.0\"", storeManifest, StringComparison.Ordinal);
        Assert.Contains("prepare-calibre-runtime.ps1", File.ReadAllText(Path.Combine(root, ".github", "workflows", "store-release.yml")), StringComparison.Ordinal);
        Assert.Contains("pagearc-store-publish", File.ReadAllText(Path.Combine(root, ".github", "workflows", "store-release.yml")), StringComparison.Ordinal);
        Assert.Contains("--inputDirectory", File.ReadAllText(Path.Combine(root, ".github", "workflows", "store-release.yml")), StringComparison.Ordinal);
        Assert.Contains("CalibreBundled", File.ReadAllText(Path.Combine(root, "Packaging", "Build-StorePackage.ps1")), StringComparison.Ordinal);
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
        Assert.Contains("App.Updates.LastResult", about, StringComparison.Ordinal);
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
