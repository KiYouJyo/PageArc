using Xunit;

namespace PageArc.Tests;

public sealed class FigmaConvergenceTests
{
    [Fact]
    public void Shell_UsesMeasuredFigmaPaneGeometryAtRuntime()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "MainWindow.FigmaConvergence.cs"));

        Assert.Contains("OpenPaneLength = 240", code, StringComparison.Ordinal);
        Assert.Contains("CompactPaneLength = 64", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Library_HasPersistentGridListControlAndNoCardFavoriteButton()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "LibraryPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Pages", "LibraryPage.ViewModes.cs"));
        var preferences = File.ReadAllText(Path.Combine(root, "Pages", "LibraryPage.Preferences.cs"));

        Assert.Contains("x:Name=\"ViewModeButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"ViewModeButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BooksListRepeater\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinItemWidth=\"258\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsStretch=\"Fill\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"Favorite_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("settings.LibraryView = _libraryView", preferences, StringComparison.Ordinal);
        Assert.Contains("BooksListRepeater.Visibility", code, StringComparison.Ordinal);
        Assert.Contains("HorizontalContentAlignment=\"Stretch\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BooksContentHost\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Library_ListRowsTrackTheAvailableViewportWidth()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "LibraryPage.ViewModes.cs"));

        Assert.Contains("BooksListRepeater.ElementPrepared", code, StringComparison.Ordinal);
        Assert.Contains("BooksScrollViewer.ViewportWidth", code, StringComparison.Ordinal);
        Assert.Contains("element.Width = width", code, StringComparison.Ordinal);
        Assert.Contains("BooksContentHost.Width = width", code, StringComparison.Ordinal);
        Assert.Contains("BooksListRepeater.Width = width", code, StringComparison.Ordinal);
        Assert.Contains("NormalizeRealizedListWidths", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Categories_UseSameResponsiveFourColumnGeometry()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "CategoriesPage.xaml"));

        Assert.Contains("MinItemWidth=\"258\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinColumnSpacing=\"26\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinRowSpacing=\"24\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsStretch=\"Fill\"", xaml, StringComparison.Ordinal);
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
