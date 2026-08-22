using Xunit;

namespace PageArc.Tests;

public sealed class ReaderRefinedInteractionFollowupTests
{
    [Fact]
    public void ReaderFrame_UsesStableLogicalCanvasForPaneAndCtrlZoomChanges()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));
        var figma = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.Figma.cs"));
        var refined = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.RefinedView.cs"));
        var reader = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml.cs"));

        Assert.Contains("x:Name=\"ReaderFrameViewport\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReaderFrameCanvas\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReaderFrameViewbox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"1600\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"900\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ReaderFrameScaleTransform", xaml, StringComparison.Ordinal);
        Assert.Contains("ReaderFrameViewbox.Width = frameWidth", figma, StringComparison.Ordinal);
        Assert.Contains("ReaderFrameCanvas.Width = Math.Max(availableWidth, frameWidth)", figma, StringComparison.Ordinal);
        Assert.Contains("ReaderFrameViewport.ChangeView", figma, StringComparison.Ordinal);
        Assert.DoesNotContain("ReflowAfterReaderResizeAsync", figma, StringComparison.Ordinal);
        Assert.DoesNotContain("body.style.zoom = String", refined, StringComparison.Ordinal);
        Assert.Contains("if (state.continuous)", reader, StringComparison.Ordinal);
    }

    [Fact]
    public void PaginatedSpread_StrideExactlyMatchesTheViewport()
    {
        var root = FindRepoRoot();
        var refined = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.RefinedView.cs"));

        Assert.Contains("const horizontalPadding = spread === 'single' ? 112 : 84", refined, StringComparison.Ordinal);
        Assert.Contains("const columnGap = spread === 'single' ? 112 : 84", refined, StringComparison.Ordinal);
        Assert.Contains("const pageStep = (columnWidth + columnGap) * visibleColumns", refined, StringComparison.Ordinal);
        Assert.Contains("padding:44px 42px 56px!important", refined, StringComparison.Ordinal);

        const double viewportWidth = 1600d;
        const double spreadPadding = 84d;
        const double spreadGap = 84d;
        const int visibleColumns = 2;
        var columnWidth = ((viewportWidth - spreadPadding) - spreadGap) / visibleColumns;
        var pageStep = (columnWidth + spreadGap) * visibleColumns;
        Assert.Equal(viewportWidth, pageStep);
    }

    [Fact]
    public void ReaderChrome_SeparatesPanesFromTheReadingCanvas()
    {
        var root = FindRepoRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml"));

        Assert.Equal(0, Count(app, "<SolidColorBrush x:Key=\"PageArcReaderPaneBrush\" Color=\"#00000000\"/>"));
        Assert.Equal(0, Count(app, "<SolidColorBrush x:Key=\"PageArcReaderAreaBrush\" Color=\"#00000000\"/>"));
        Assert.Contains("PageArcReaderDividerBrush", app, StringComparison.Ordinal);
        Assert.Contains("<SolidColorBrush x:Key=\"PageArcReaderPageBrush\" Color=\"#F5FFFFFF\"/>", app, StringComparison.Ordinal);
        Assert.Contains("<SolidColorBrush x:Key=\"PageArcReaderPageBrush\" Color=\"#FA1F1F1F\"/>", app, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectionNotePopup_IsButtonlessAutosavingAndFlushesOnDismissal()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.SelectionAnnotations.cs"));

        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));
        Assert.DoesNotContain("AnnotationPopupClose_Click", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveSelectionAnnotationButton", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectionAnnotationCard.Height = 74", code, StringComparison.Ordinal);
        Assert.Contains("SelectionAnnotationTextBox.TextChanged += SelectionAnnotationTextBox_TextChanged", code, StringComparison.Ordinal);
        Assert.Contains("await Task.Delay(400, cancellationToken)", code, StringComparison.Ordinal);
        Assert.Contains("App.ReadingData.SaveAnnotation", code, StringComparison.Ordinal);
        Assert.Contains("_selectionAnnotationId ?? Guid.NewGuid()", code, StringComparison.Ordinal);
        Assert.Contains("CloseSelectionPopupAsync(saveBeforeClose: true)", code, StringComparison.Ordinal);
        Assert.Contains("SelectionAnnotationPopup.IsLightDismissEnabled = true", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingHighlight_ClickOpensItsOriginalNoteForEditing()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.SelectionAnnotations.cs"));

        Assert.Contains("annotation-edit:", code, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation()", code, StringComparison.Ordinal);
        Assert.Contains("item.Id, payload.AnnotationId", code, StringComparison.Ordinal);
        Assert.Contains("ShowSelectionAnnotationPopup(payload, annotation)", code, StringComparison.Ordinal);
        Assert.Contains("SelectionAnnotationTextBox.Text = existing?.Note ?? string.Empty", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderMeasuresEverySectionBeforeFreezingThePageTotal()
    {
        var root = FindRepoRoot();
        var reader = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml.cs"));
        var map = File.ReadAllText(Path.Combine(root, "Services", "FlowPageMap.cs"));

        Assert.Contains("await MeasureAndFreezePageMapAsync()", reader, StringComparison.Ordinal);
        Assert.Contains("for (var index = 0; index < _document.Sections.Count; index++)", reader, StringComparison.Ordinal);
        Assert.Contains("await document.fonts?.ready", reader, StringComparison.Ordinal);
        Assert.Contains("Promise.all(Array.from(document.images", reader, StringComparison.Ordinal);
        Assert.Contains("map.FreezeMeasuredPages(measuredPages)", reader, StringComparison.Ordinal);
        Assert.Contains("if (IsFrozen) return", map, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewOptions_AreDirectlyAvailableInTheRightSettingsPane()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml"));
        var refined = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.RefinedView.cs"));

        Assert.Contains("x:Name=\"ReaderViewOptionsLabel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OddPageStartButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"EvenPageStartButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SinglePageButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FitPageWidthButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"FitPageHeightButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("UpdateReaderViewOptionSelection", refined, StringComparison.Ordinal);
        Assert.Contains("EnforceFixedReaderOptions", refined, StringComparison.Ordinal);
        Assert.Contains("settings.ReaderViewMode = \"horizontal\"", refined, StringComparison.Ordinal);
        Assert.DoesNotContain("settings.ReaderViewMode = \"vertical\"", refined, StringComparison.Ordinal);
        Assert.Contains("ApplyFigmaReaderPageGeometry", refined, StringComparison.Ordinal);
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
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
