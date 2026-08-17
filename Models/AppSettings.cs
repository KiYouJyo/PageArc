namespace PageArc.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "system";
    public string AppTheme { get; set; } = "system";
    public string AccentSource { get; set; } = "windows";
    public string ReadingTheme { get; set; } = "light";
    // The Figma reader exposes Light/Sepia/Dark as explicit overrides. Until the user
    // chooses one, the reading surface follows the effective app Light/Dark theme.
    public bool ReadingThemeFollowsApp { get; set; } = true;
    public string DefaultFont { get; set; } = "book";
    public double FontScale { get; set; } = 1.0;
    public double LineHeight { get; set; } = 1.75;
    public string PageWidth { get; set; } = "medium";
    public bool ContinuousScrolling { get; set; }
    public bool ShowReadingProgress { get; set; } = true;
    public bool ShowRecentBooks { get; set; } = true;
    public bool DuplicateDetection { get; set; } = true;
    public string LibrarySort { get; set; } = "recent";
    public string LibraryFilter { get; set; } = "all";
    public string LibraryView { get; set; } = "grid";
}
