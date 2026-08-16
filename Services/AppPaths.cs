namespace PageArc.Services;

public static class AppPaths
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PageArc");

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string LibraryFile => Path.Combine(Root, "library.json");
    public static string CategoriesFile => Path.Combine(Root, "categories.json");
    public static string CacheRoot => Path.Combine(Root, "Cache");
    public static string BooksCacheRoot => Path.Combine(CacheRoot, "Books");

    public static void Ensure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(CacheRoot);
        Directory.CreateDirectory(BooksCacheRoot);
    }
}
