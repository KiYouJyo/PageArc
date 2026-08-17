using Xunit;

namespace PageArc.Tests;

public sealed class ReaderRefinedInteractionFollowupTests
{
    [Fact]
    public void ReaderChrome_RevealsTheSameBackdropAsTheCustomTitleBar()
    {
        var root = FindRepoRoot();
        var app = File.ReadAllText(Path.Combine(root, "App.xaml"));

        Assert.Equal(2, Count(app, "<SolidColorBrush x:Key=\"PageArcReaderToolbarBrush\" Color=\"#00000000\"/>"));
        Assert.Equal(2, Count(app, "<SolidColorBrush x:Key=\"PageArcReaderPaneBrush\" Color=\"#00000000\"/>"));
        Assert.Equal(2, Count(app, "<SolidColorBrush x:Key=\"PageArcReaderAreaBrush\" Color=\"#00000000\"/>"));
        Assert.Contains("<SolidColorBrush x:Key=\"PageArcReaderPageBrush\" Color=\"#F5FFFFFF\"/>", app, StringComparison.Ordinal);
        Assert.Contains("<SolidColorBrush x:Key=\"PageArcReaderPageBrush\" Color=\"#FA1F1F1F\"/>", app, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectionNotePopup_IsButtonlessAutosavingAndFlushesOnDismissal()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.SelectionAnnotations.cs"));

        Assert.Contains("annotationHeader.Visibility = Visibility.Collapsed", code, StringComparison.Ordinal);
        Assert.Contains("annotationFooter.Visibility = Visibility.Collapsed", code, StringComparison.Ordinal);
        Assert.Contains("SelectionAnnotationCard.Height = 102", code, StringComparison.Ordinal);
        Assert.Contains("SelectionAnnotationTextBox.TextChanged += SelectionAnnotationTextBox_TextChanged", code, StringComparison.Ordinal);
        Assert.Contains("await Task.Delay(400, cancellationToken)", code, StringComparison.Ordinal);
        Assert.Contains("App.ReadingData.SaveAnnotation", code, StringComparison.Ordinal);
        Assert.Contains("_selectionAnnotationId ?? Guid.NewGuid()", code, StringComparison.Ordinal);
        Assert.Contains("CloseSelectionPopupAsync(saveBeforeClose: true)", code, StringComparison.Ordinal);
        Assert.Contains("SelectionAnnotationPopup.IsLightDismissEnabled = true", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewModeSelector_RetriesWhenRightSettingsPaneActuallyLoadsOrOpens()
    {
        var root = FindRepoRoot();
        var selectionCode = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.SelectionAnnotations.cs"));
        var refined = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.RefinedView.cs"));

        Assert.Contains("ReaderSettingsPane.Loaded +=", selectionCode, StringComparison.Ordinal);
        Assert.Contains("AppearanceButton.Click +=", selectionCode, StringComparison.Ordinal);
        Assert.Contains("EnsureReaderViewModeControls", selectionCode, StringComparison.Ordinal);
        Assert.Contains("BuildReaderViewModeControls", refined, StringComparison.Ordinal);
        Assert.Contains("查看方式", refined, StringComparison.Ordinal);
        Assert.Contains("垂直滚动", refined, StringComparison.Ordinal);
        Assert.Contains("水平滚动", refined, StringComparison.Ordinal);
        Assert.Contains("覆盖滚动", refined, StringComparison.Ordinal);
        Assert.Contains("奇数页起始（无封面）", refined, StringComparison.Ordinal);
        Assert.Contains("偶数页起始（有封面）", refined, StringComparison.Ordinal);
        Assert.Contains("适应页面宽度", refined, StringComparison.Ordinal);
        Assert.Contains("适应页面高度", refined, StringComparison.Ordinal);
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
