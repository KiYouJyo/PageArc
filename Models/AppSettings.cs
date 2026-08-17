namespace PageArc.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "system";
    public string AppTheme { get; set; } = "system";
    public string ReadingTheme { get; set; } = "light";
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
