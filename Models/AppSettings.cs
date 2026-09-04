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
    // Continuous reading and the progress strip are fixed reader behavior. The fields remain
    // persisted for compatibility with older settings files and are normalized on reader load.
    public string ReaderViewMode { get; set; } = "vertical";
    public string ReaderSpreadMode { get; set; } = "single";
    public string ReaderZoomMode { get; set; } = "auto";
    public double ReaderZoomFactor { get; set; } = 1.0;
    public bool ContinuousScrolling { get; set; } = true;
    public bool ShowReadingProgress { get; set; } = true;
    public bool ClickToTurnPages { get; set; } = true;
    public bool ShowRecentBooks { get; set; } = true;
    public bool DuplicateDetection { get; set; } = true;
    public string WebDavEndpoint { get; set; } = string.Empty;
    public string WebDavUsername { get; set; } = string.Empty;
    public DateTimeOffset? WebDavLastSyncAt { get; set; }
    public string LibrarySort { get; set; } = "recent";
    public string LibraryFilter { get; set; } = "all";
    public string LibraryView { get; set; } = "grid";
    public bool HasWindowPlacement { get; set; }
    public int LastNormalWindowX { get; set; }
    public int LastNormalWindowY { get; set; }
    public int LastNormalWindowWidth { get; set; }
    public int LastNormalWindowHeight { get; set; }
    public bool WasWindowMaximized { get; set; }
}
