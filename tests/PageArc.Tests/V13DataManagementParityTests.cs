using Xunit;

namespace PageArc.Tests;

public sealed class V13DataManagementParityTests
{
    private const string UrbanPlanToolboxSourceSha = "249bbf99088e5edc92b9a6f9b7635ca777cf847e";

    [Fact]
    public void DataManagementCard_CopiesUrbanPlanToolboxStructureAndSpacing()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml"));

        Assert.Contains(UrbanPlanToolboxSourceSha, xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DataManagementCard\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource SettingsSectionCardStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<StackPanel Spacing=\"16\">", xaml, StringComparison.Ordinal);
        Assert.Contains("<StackPanel Spacing=\"4\">", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DataManagementGrid\" ColumnSpacing=\"12\"", xaml, StringComparison.Ordinal);

        Assert.Contains("x:Name=\"DataLocalPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DataCloudPanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{ThemeResource ControlFillColorDefaultBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BorderBrush=\"{ThemeResource ControlStrokeColorDefaultBrush}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BorderThickness=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CornerRadius=\"8\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Padding=\"14\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<StackPanel Spacing=\"12\">", xaml, StringComparison.Ordinal);
        Assert.Contains("<StackPanel Spacing=\"3\">", xaml, StringComparison.Ordinal);
        Assert.Contains("<StackPanel Spacing=\"2\">", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DataManagementActions_CopyUrbanPlanToolboxOrderAndDefaultSizing()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml"));

        Assert.Contains("x:Name=\"DataActions\" HorizontalAlignment=\"Left\" ColumnSpacing=\"8\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ExportButton\" Grid.Column=\"0\" Style=\"{StaticResource AccentButtonStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ImportButton\" Grid.Column=\"1\" Style=\"{StaticResource AccentButtonStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ClearDataButton\" Grid.Column=\"2\" HorizontalAlignment=\"Left\"", xaml, StringComparison.Ordinal);

        Assert.Contains("x:Name=\"WebDavBackupButton\" Grid.Column=\"0\" Style=\"{StaticResource AccentButtonStyle}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WebDavRestoreButton\" Grid.Column=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WebDavManageButton\" Grid.Column=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WebDavConfigureButton\" Grid.Column=\"3\"", xaml, StringComparison.Ordinal);

        var dataStart = xaml.IndexOf("x:Name=\"DataManagementCard\"", StringComparison.Ordinal);
        var stateStart = xaml.IndexOf("<VisualStateManager.VisualStateGroups>", dataStart, StringComparison.Ordinal);
        Assert.True(dataStart >= 0 && stateStart > dataStart);
        var dataBlock = xaml[dataStart..stateStart];
        Assert.DoesNotContain("MinWidth=\"112\"", dataBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("MinWidth=\"92\"", dataBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Height=\"36\"", dataBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Padding=\"20\"", dataBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("WebDavProgressRing", dataBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void DataManagementResponsiveRules_CopyUrbanPlanToolboxBreakpoints()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.xaml"));

        Assert.Contains("x:Name=\"DataManagementResponsiveStates\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"DataManagementCompact\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<AdaptiveTrigger MinWindowWidth=\"0\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("Target=\"DataLocalPanel.(Grid.ColumnSpan)\" Value=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Target=\"DataCloudPanel.(Grid.Row)\" Value=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Target=\"DataCloudPanel.Margin\" Value=\"0,12,0,0\"", xaml, StringComparison.Ordinal);

        Assert.Contains("x:Name=\"DataManagementWide\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<AdaptiveTrigger MinWindowWidth=\"520\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("Target=\"DataLocalPanel.(Grid.ColumnSpan)\" Value=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Target=\"DataCloudPanel.(Grid.Row)\" Value=\"0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Target=\"DataCloudPanel.(Grid.Column)\" Value=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Target=\"DataCloudPanel.Margin\" Value=\"0\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DataManagementStyles_CopyUrbanPlanToolboxTokens()
    {
        var root = FindRepoRoot();
        var resources = File.ReadAllText(Path.Combine(root, "App.xaml"));

        Assert.Contains(UrbanPlanToolboxSourceSha, resources, StringComparison.Ordinal);
        Assert.Contains("<Thickness x:Key=\"CardContentPadding\">16</Thickness>", resources, StringComparison.Ordinal);
        Assert.Contains("<CornerRadius x:Key=\"CardCornerRadius\">8</CornerRadius>", resources, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsSectionCardStyle\"", resources, StringComparison.Ordinal);
        Assert.Contains("CardBackgroundFillColorDefaultBrush", resources, StringComparison.Ordinal);
        Assert.Contains("CardStrokeColorDefaultBrush", resources, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsRowLabelStyle\"", resources, StringComparison.Ordinal);
        Assert.Contains("BasedOn=\"{StaticResource BodyStrongTextBlockStyle}\"", resources, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsDescriptionStyle\"", resources, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Opacity\" Value=\"0.72\" />", resources, StringComparison.Ordinal);
    }

    [Fact]
    public void V14VersionContract_IsConsistent()
    {
        var root = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "PageArc.csproj"));
        var githubManifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        var acceptanceManifest = File.ReadAllText(Path.Combine(root, "Packaging", "PageArc.Package.appxmanifest"));
        var storeManifest = File.ReadAllText(Path.Combine(root, "Package.Store.appxmanifest"));

        Assert.Contains("<Version>1.4.0</Version>", project, StringComparison.Ordinal);
        Assert.Contains("<AssemblyVersion>1.4.0.0</AssemblyVersion>", project, StringComparison.Ordinal);
        Assert.Contains("<ApplicationDisplayVersion>1.4</ApplicationDisplayVersion>", project, StringComparison.Ordinal);
        Assert.Contains("Version=\"1.4.0.0\"", githubManifest, StringComparison.Ordinal);
        Assert.Contains("Version=\"1.4.0.0\"", acceptanceManifest, StringComparison.Ordinal);
        Assert.Contains("Version=\"2026.904.140.0\"", storeManifest, StringComparison.Ordinal);
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
