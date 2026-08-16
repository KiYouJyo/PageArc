using System.Collections.ObjectModel;
using System.Text.Json;
using PageArc.Models;

namespace PageArc.Services;

public sealed class LibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();

    public ObservableCollection<BookEntry> Books { get; } = [];

    public void Load()
    {
        AppPaths.Ensure();
        try
        {
            if (!File.Exists(AppPaths.LibraryFile)) return;
            var items = JsonSerializer.Deserialize<List<BookEntry>>(File.ReadAllText(AppPaths.LibraryFile)) ?? [];
            Books.Clear();
            foreach (var item in items.Where(x => File.Exists(x.FilePath))) Books.Add(item);
        }
        catch
        {
            Books.Clear();
        }
    }

    public async Task<BookEntry> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("Book file not found.", filePath);
        var fullPath = Path.GetFullPath(filePath);

        lock (_gate)
        {
            var existing = Books.FirstOrDefault(x =>
                string.Equals(Path.GetFullPath(x.FilePath), fullPath, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) return existing;
        }

        var info = new FileInfo(fullPath);
        var extension = info.Extension.TrimStart('.').ToUpperInvariant();
        var title = Path.GetFileNameWithoutExtension(fullPath);
        var author = string.Empty;

        if (extension == "EPUB")
        {
            try
            {
                var metadata = await EpubParser.ReadMetadataAsync(fullPath, cancellationToken);
                if (!string.IsNullOrWhiteSpace(metadata.Title)) title = metadata.Title;
                author = metadata.Author;
            }
            catch
            {
                // Keep filename metadata. Opening the book will surface the parsing error.
            }
        }

        var entry = new BookEntry
        {
            FilePath = fullPath,
            Format = extension,
            Title = title,
            Author = author,
            FileSize = info.Length
        };

        Books.Add(entry);
        Save();
        return entry;
    }

    public void MarkOpened(BookEntry book)
    {
        book.LastOpenedAt = DateTimeOffset.Now;
        Save();
    }

    public void Save()
    {
        lock (_gate)
        {
            AppPaths.Ensure();
            var temp = AppPaths.LibraryFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Books.ToList(), JsonOptions));
            File.Move(temp, AppPaths.LibraryFile, true);
        }
    }
}
