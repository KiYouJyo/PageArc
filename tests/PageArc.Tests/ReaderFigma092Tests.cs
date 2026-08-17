using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class ReaderFigma092Tests
{
    [Fact]
    public void ReaderDefaults_FollowTheEffectiveAppThemeUntilExplicitlyOverridden()
    {
        var settings = new AppSettings();
        Assert.True(settings.ReadingThemeFollowsApp);
        Assert.Equal("light", settings.ReadingTheme);
    }

    [Fact]
    public void ReaderThemeFollowPreference_RoundTripsThroughSettingsService()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-reader-theme-{Guid.NewGuid():N}");
        var file = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(root);
        try
        {
            var service = new SettingsService(file);
            service.Load();
            service.Update(settings =>
            {
                settings.ReadingTheme = "sepia";
                settings.ReadingThemeFollowsApp = false;
            });

            var reloaded = new SettingsService(file);
            reloaded.Load();
            Assert.Equal("sepia", reloaded.Current.ReadingTheme);
            Assert.False(reloaded.Current.ReadingThemeFollowsApp);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReaderChrome_MatchesMeasuredFigmaGeometry()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));

        Assert.Contains("<RowDefinition Height=\"48\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ContentsColumn\" Width=\"260\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SidebarToggleButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"86\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ContentsModeButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SearchModeButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BookmarksModeButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NotesModeButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxWidth=\"760\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MaxHeight=\"704\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"60,28,60,72\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,338,0,0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReaderProgress\" Width=\"420\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PageJumpBox\" Width=\"50\" Height=\"28\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"BackButton\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"BookmarkButton\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderSettingsFlyout_MatchesFigmaControlInventoryAndSize()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));

        Assert.Contains("Width=\"336\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"620\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ThemeLightButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ThemeSepiaButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ThemeDarkButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FigmaFontFamilyCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReaderFontScaleSlider\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"LineNormalButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WidthMediumButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ContinuousScrollToggle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FigmaShowProgressToggle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FigmaResetReaderButton\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderThemeSync_TracksActualThemeAndPreservesExplicitOverrides()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.Figma.cs"));
        var settingsCode = File.ReadAllText(Path.Combine(root, "Pages", "SettingsPage.ReaderTheme.cs"));

        Assert.Contains("ReaderRootGrid.ActualTheme == ElementTheme.Dark ? \"dark\" : \"light\"", code, StringComparison.Ordinal);
        Assert.Contains("settings.ReadingThemeFollowsApp = false", code, StringComparison.Ordinal);
        Assert.Contains("settings.ReadingThemeFollowsApp = true", code, StringComparison.Ordinal);
        Assert.Contains("ApplyWebReaderStyleAsync", code, StringComparison.Ordinal);
        Assert.Contains("settings.ReadingThemeFollowsApp = false", settingsCode, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderDarkTheme_UsesNeutralDarkFigmaSurfaceTokens()
    {
        var root = FindRepoRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml"));
        Assert.Contains("PageArcReaderAreaBrush", app, StringComparison.Ordinal);
        Assert.Contains("Color=\"#FF202020\"", app, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PageArcReaderPageBrush", app, StringComparison.Ordinal);
        Assert.Contains("Color=\"#FA1F1F1F\"", app, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PageArc.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate PageArc repository root.");
    }
}
