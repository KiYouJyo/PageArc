using PageArc.Models;

namespace PageArc.Services;

public static class CacheMaintenanceService
{
    public static int ClearGeneratedCache(string cacheRoot, IEnumerable<BookEntry> books)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRoot);
        ArgumentNullException.ThrowIfNull(books);

        var fullRoot = Path.GetFullPath(cacheRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var changed = 0;

        foreach (var book in books)
        {
            if (string.IsNullOrWhiteSpace(book.CoverPath)) continue;
            try
            {
                var cover = Path.GetFullPath(book.CoverPath);
                if (!cover.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) continue;
                book.CoverPath = null;
                changed++;
            }
            catch
            {
                // Invalid legacy paths should not block cache maintenance.
            }
        }

        if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
        Directory.CreateDirectory(cacheRoot);
        return changed;
    }

    public static int ClearGeneratedCache(IEnumerable<BookEntry> books)
    {
        var changed = ClearGeneratedCache(AppPaths.CacheRoot, books);
        AppPaths.Ensure();
        return changed;
    }
}
