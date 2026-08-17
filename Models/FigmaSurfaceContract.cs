namespace PageArc.Models;

public sealed record FigmaSurfaceContract(
    string NodeId,
    string Name,
    string FunctionalOwner,
    bool IsFunctionallyComplete);

public static class PageArcFigmaSurfaces
{
    public static IReadOnlyList<FigmaSurfaceContract> Canonical { get; } =
    [
        new("16:3", "Library", "LibraryPage", true),
        new("16:156", "Reader", "ReaderPage", true),
        new("16:227", "Reader settings", "ReaderPage", true),
        new("16:346", "Library collapsed sidebar", "MainWindow/LibraryPage", true),
        new("16:499", "Library empty state", "LibraryPage", true),
        new("16:569", "Library search results", "LibraryPage", true),
        new("16:666", "Book context menu", "LibraryPage", true),
        new("16:842", "Book details", "LibraryPage", true),
        new("16:1025", "Reader bookmarks", "ReaderPage", true),
        new("16:1088", "Reader notes", "ReaderPage", true),
        new("16:1149", "Reader search", "ReaderPage", true),
        new("16:1217", "Import chooser", "LibraryPage.Import", true),
        new("16:1386", "Import progress", "LibraryPage.Import", true),
        new("16:1580", "Import completion", "LibraryPage.Import", true),
        new("16:1750", "Settings", "SettingsPage", true),
        new("16:1878", "About", "AboutPage", true),
        new("16:2206", "Categories", "CategoriesPage", true),
        new("16:2294", "Import folders", "ImportFoldersPage", true),
        new("25:2", "Format conversion", "ConversionPage", true)
    ];
}
