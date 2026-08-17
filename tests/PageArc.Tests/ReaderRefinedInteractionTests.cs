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
        Assert.Contains("PageArcReaderToolbarBrush\" Color=\"#FFF6F6F6", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PageArcReaderPaneBrush\" Color=\"#FFF6F6F6", appXaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefinedReader_HasWheelHalfPageViewModesAnimationsAndNoteOnlyHighlight()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.RefinedView.cs"));
        var selection = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.SelectionAnnotations.cs"));

        Assert.Contains("document.addEventListener('wheel'", code, StringComparison.Ordinal);
        Assert.Contains("document.addEventListener('click'", code, StringComparison.Ordinal);
        Assert.Contains("pagearc-turn:", code, StringComparison.Ordinal);
        Assert.Contains("AnimateReaderColumnAsync", code, StringComparison.Ordinal);
        Assert.Contains("view:vertical", code, StringComparison.Ordinal);
        Assert.Contains("view:horizontal", code, StringComparison.Ordinal);
        Assert.Contains("view:wrapped", code, StringComparison.Ordinal);
        Assert.Contains("spread:odd", code, StringComparison.Ordinal);
        Assert.Contains("spread:even", code, StringComparison.Ordinal);
        Assert.Contains("zoom:fit-width", code, StringComparison.Ordinal);
        Assert.Contains("zoom:fit-height", code, StringComparison.Ordinal);
        Assert.Contains("rgba(185,111,111,.30)", code, StringComparison.Ordinal);
        Assert.Contains("Write a note before saving", selection, StringComparison.Ordinal);
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
