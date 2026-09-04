namespace PageArc.Services;

public static class AppPaths
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PageArc");

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string LibraryFile => Path.Combine(Root, "library.json");
    public static string CategoriesFile => Path.Combine(Root, "categories.json");
    public static string ReadingDataFile => Path.Combine(Root, "reading-data.json");
    public static string ImportFoldersFile => Path.Combine(Root, "import-folders.json");
    public static string ShellSessionFile => Path.Combine(Root, "shell-session.json");
    public static string ManagedLibraryRoot => Path.Combine(Root, "Library");
    public static string ManagedBooksRoot => Path.Combine(ManagedLibraryRoot, "Books");
    public static string RuntimesRoot => Path.Combine(Root, "Runtimes");
    public static string ConversionRuntimesRoot => Path.Combine(RuntimesRoot, "Conversion");
    public static string RuntimeDownloadsRoot => Path.Combine(RuntimesRoot, "Downloads");
    public static string CacheRoot => Path.Combine(Root, "Cache");
    public static string BooksCacheRoot => Path.Combine(CacheRoot, "Books");
    public static string NormalizedBooksRoot => Path.Combine(CacheRoot, "NormalizedBooks");
    public static string KindleParserRoot => Path.Combine(CacheRoot, "KindleParser");
    public static string CoversRoot => Path.Combine(CacheRoot, "Covers");

    public static void Ensure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ManagedLibraryRoot);
        Directory.CreateDirectory(ManagedBooksRoot);
        Directory.CreateDirectory(RuntimesRoot);
        Directory.CreateDirectory(ConversionRuntimesRoot);
        Directory.CreateDirectory(RuntimeDownloadsRoot);
        Directory.CreateDirectory(CacheRoot);
        Directory.CreateDirectory(BooksCacheRoot);
        Directory.CreateDirectory(NormalizedBooksRoot);
        Directory.CreateDirectory(KindleParserRoot);
        Directory.CreateDirectory(CoversRoot);
    }
}
