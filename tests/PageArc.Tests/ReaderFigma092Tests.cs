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
    public void ReaderChrome_MatchesRefinedFigmaGeometry()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));
        var refined = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.RefinedView.cs"));

        Assert.Contains("<RowDefinition Height=\"48\"/>", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ContentsColumn\" Width=\"260\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SettingsColumn\" Width=\"0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SidebarToggleButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ContentsModeButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SearchModeButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BookmarksModeButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NotesModeButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"12,12,12,0\" Height=\"36\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment\" Value=\"Stretch\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CharacterSpacing=\"40\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReaderSurface\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReaderFrameViewport\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReaderFrameCanvas\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReaderFrameViewbox\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ReaderFrameScaleTransform", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"1600\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"900\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"24,20,24,68\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReaderProgress\" Grid.Column=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PageJumpBox\" Grid.Column=\"5\" Width=\"48\" Height=\"28\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReaderProgress\" Grid.Column=\"2\" Height=\"30\" Padding=\"10,0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\" MaxLines=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PreviousPageButton.Visibility = Visibility.Collapsed", refined, StringComparison.Ordinal);
        Assert.Contains("NextPageButton.Visibility = Visibility.Collapsed", refined, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"BackButton\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"BookmarkButton\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderSettings_UsesAnimatedFullHeightRightPaneInsteadOfFlyout()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.Figma.cs"));
        var refined = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.RefinedView.cs"));

        Assert.Contains("x:Name=\"ReaderSettingsPane\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"260\"", xaml, StringComparison.Ordinal);
        Assert.Contains("BorderThickness=\"1,0,0,0\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<Button.Flyout>", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"AppearanceButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AnimateRightSidebarAsync(open)", code, StringComparison.Ordinal);
        Assert.Contains("AnimateReaderColumnAsync", refined, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ThemeLightButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ThemeSepiaButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ThemeDarkButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"ReaderThemeCardStyle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Property=\"BorderThickness\" Value=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FigmaFontFamilyCombo\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReaderFontScaleSlider\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"LineNormalButton\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"WidthMediumButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OddPageStartButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"EvenPageStartButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SinglePageButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FitPageWidthButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FitPageHeightButton\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ContinuousScrollToggle", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FigmaShowProgressToggle", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FigmaResetReaderButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ClickPageTurnToggle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Toggled=\"ClickPageTurnToggle_Toggled\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectionAnnotations_UseContextualNoteOnlyPopupInsteadOfTopMenuCommands()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));
        var selectionCode = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.SelectionAnnotations.cs"));

        Assert.Contains("x:Name=\"SelectionAnnotationPopup\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SelectionAnnotationTextBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("window.getSelection", selectionCode, StringComparison.Ordinal);
        Assert.DoesNotContain("HighlightYellowButton", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("AnnotationPopupClose_Click", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"360\"", xaml, StringComparison.Ordinal);
        Assert.Contains("App.ReadingData.SaveAnnotation", selectionCode, StringComparison.Ordinal);
        Assert.Contains("note-red", selectionCode, StringComparison.Ordinal);
        Assert.Contains("MoreButton.Flyout = null", selectionCode, StringComparison.Ordinal);
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
        Assert.Contains("settings.ReadingThemeFollowsApp = theme == \"app\"", settingsCode, StringComparison.Ordinal);
        Assert.Contains("ActualTheme == ElementTheme.Dark ? \"dark\" : \"light\"", settingsCode, StringComparison.Ordinal);
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
