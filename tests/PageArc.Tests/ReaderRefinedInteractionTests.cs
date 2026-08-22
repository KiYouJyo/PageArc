using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class ReaderRefinedInteractionTests
{
    [Fact]
    public void RefinedReaderChrome_MatchesCompactFigmaContract()
    {
        var root = FindRepoRoot();
        var shellXaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        var shellCode = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var appXaml = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var readerCode = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.RefinedView.cs"));

        Assert.Contains("Height=\"32\"", shellXaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,8,12,8\"", shellXaml, StringComparison.Ordinal);
        Assert.Contains("FontSize = 12", shellCode, StringComparison.Ordinal);
        Assert.Contains("new FontIcon", shellCode, StringComparison.Ordinal);
        Assert.Contains("Height = 32", shellCode, StringComparison.Ordinal);
        Assert.Contains("MoreButton.Visibility = Visibility.Collapsed", readerCode, StringComparison.Ordinal);
        Assert.Contains("PreviousPageButton.Visibility = Visibility.Collapsed", readerCode, StringComparison.Ordinal);
        Assert.Contains("NextPageButton.Visibility = Visibility.Collapsed", readerCode, StringComparison.Ordinal);
        Assert.Contains("PageArcReaderDividerBrush", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PageArcReaderPaneBrush\" Color=\"#00000000", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PageArcReaderAreaBrush\" Color=\"#00000000", appXaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefinedReader_HasWheelHalfPageViewModesAnimationsAndNoteOnlyHighlight()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.RefinedView.cs"));
        var selection = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.SelectionAnnotations.cs"));

        Assert.Contains("document.addEventListener('wheel'", code, StringComparison.Ordinal);
        Assert.Contains("document.addEventListener('click'", code, StringComparison.Ordinal);
        Assert.Contains("window.__pagearcClickToTurn === false", code, StringComparison.Ordinal);
        Assert.Contains("ClickPageTurnToggle_Toggled", code, StringComparison.Ordinal);
        Assert.Contains("pagearc-turn:", code, StringComparison.Ordinal);
        Assert.Contains("AnimateReaderColumnAsync", code, StringComparison.Ordinal);
        Assert.DoesNotContain("ReflowAfterReaderResizeAsync", code, StringComparison.Ordinal);
        Assert.Contains("ApplyFigmaReaderPageGeometry();", code, StringComparison.Ordinal);
        Assert.Contains("ReaderViewOption_Click", code, StringComparison.Ordinal);
        Assert.Contains("spread:odd", code, StringComparison.Ordinal);
        Assert.Contains("spread:even", code, StringComparison.Ordinal);
        Assert.Contains("spread:single", code, StringComparison.Ordinal);
        Assert.Contains("spread:odd", code, StringComparison.Ordinal);
        Assert.Contains("spread:even", code, StringComparison.Ordinal);
        Assert.Contains("zoom:fit-width", code, StringComparison.Ordinal);
        Assert.Contains("zoom:fit-height", code, StringComparison.Ordinal);
        Assert.Contains("pagearc-spread-blank", code, StringComparison.Ordinal);
        Assert.Contains("pagearc-image-page", code, StringComparison.Ordinal);
        Assert.Contains("column-count:1!important", code, StringComparison.Ordinal);
        Assert.Contains("column-count:2!important", code, StringComparison.Ordinal);
        Assert.Contains("pagearc-spread-right", code, StringComparison.Ordinal);
        Assert.Contains("const horizontalPadding = spread === 'single' ? 112 : 84", code, StringComparison.Ordinal);
        Assert.Contains("const columnGap = spread === 'single' ? 112 : 84", code, StringComparison.Ordinal);
        Assert.Contains("const visibleColumns = spread === 'single' ? 1 : 2", code, StringComparison.Ordinal);
        Assert.Contains("root.clientWidth - horizontalPadding", code, StringComparison.Ordinal);
        Assert.Contains("setProperty('column-width'", code, StringComparison.Ordinal);
        Assert.Contains("const pageStep = (columnWidth + columnGap) * visibleColumns", code, StringComparison.Ordinal);
        Assert.Contains("window.__pagearc.pageStep = pageStep", code, StringComparison.Ordinal);
        Assert.Contains("window.__pagearc.visiblePageCount = visibleColumns", code, StringComparison.Ordinal);
        Assert.Contains("padding:44px 42px 56px!important", code, StringComparison.Ordinal);
        Assert.DoesNotContain("window.__pagearc.pageStep = root.clientWidth", code, StringComparison.Ordinal);
        var reader = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml.cs"));
        Assert.Contains("pagearc-progress:", reader, StringComparison.Ordinal);
        Assert.Contains("ReaderLayoutPolicy.ResolveSpreadStartIndex", reader, StringComparison.Ordinal);
        Assert.Contains("CreateLeadingBlankSpread", reader, StringComparison.Ordinal);
        Assert.Contains("body :where(p,div,li,dd,dt,blockquote){line-height:__LINE_HEIGHT__!important;}", reader, StringComparison.Ordinal);
        Assert.Contains("NavigateToSectionAsync(_sectionIndex, restoreSavedFraction: false)", code, StringComparison.Ordinal);
        Assert.Contains("Math.round(target / step)", File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml.cs")), StringComparison.Ordinal);
        Assert.Contains("state.snap()", reader, StringComparison.Ordinal);
        Assert.Contains("behavior:'auto'", reader, StringComparison.Ordinal);
        var geometry = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.Figma.cs"));
        Assert.Contains("ReaderFrameViewbox.Width = frameWidth", geometry, StringComparison.Ordinal);
        Assert.Contains("ReaderFrameViewbox.Height = frameHeight", geometry, StringComparison.Ordinal);
        Assert.Contains("ReaderFrameViewport.ChangeView", geometry, StringComparison.Ordinal);
        Assert.DoesNotContain("body.style.zoom = String", code, StringComparison.Ordinal);
        Assert.Contains("pagearc-zoom:", code, StringComparison.Ordinal);
        Assert.Contains("event.ctrlKey", code, StringComparison.Ordinal);
        Assert.DoesNotContain("GetReaderCoverDataUrlAsync", code, StringComparison.Ordinal);
        Assert.DoesNotContain("padding-left:calc(50vw", code, StringComparison.Ordinal);
        Assert.DoesNotContain("50vw - 70px", code, StringComparison.Ordinal);
        Assert.Contains("rgba(185,111,111,.30)", code, StringComparison.Ordinal);
        Assert.Contains("SelectionAnnotationTextBox.TextChanged", selection, StringComparison.Ordinal);
        Assert.Contains("Task.Delay(400", selection, StringComparison.Ordinal);
        Assert.Contains("note-red", selection, StringComparison.Ordinal);
    }

    [Fact]
    public void RefinedReaderViewPreferences_RoundTripThroughSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-reader-view-{Guid.NewGuid():N}");
        var file = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(root);
        try
        {
            var service = new SettingsService(file);
            service.Load();
            service.Update(settings =>
            {
                settings.ReaderViewMode = "wrapped";
                settings.ReaderSpreadMode = "even";
                settings.ReaderZoomMode = "fit-width";
                settings.ReaderZoomFactor = 1.3;
            });

            var reloaded = new SettingsService(file);
            reloaded.Load();
            Assert.Equal("wrapped", reloaded.Current.ReaderViewMode);
            Assert.Equal("even", reloaded.Current.ReaderSpreadMode);
            Assert.Equal("fit-width", reloaded.Current.ReaderZoomMode);
            Assert.Equal(1.3, reloaded.Current.ReaderZoomFactor, 3);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
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
