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
            foreach (var item in items.Where(x => File.Exists(x.FilePath)))
            {
                var normalized = BookFormatRegistry.Normalize(item.Format);
                if (string.IsNullOrWhiteSpace(normalized)) normalized = BookFormatRegistry.FormatFromPath(item.FilePath);
                if (!string.IsNullOrWhiteSpace(normalized)) item.Format = normalized;
                item.SectionFraction = Math.Clamp(item.SectionFraction, 0, 1);
                item.Progress = Math.Clamp(item.Progress, 0, 1);
                Books.Add(item);
            }
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
        var format = BookFormatRegistry.FormatFromPath(fullPath);
        if (string.IsNullOrWhiteSpace(format))
            throw new NotSupportedException($"Unsupported ebook format: {info.Extension}");

        var title = Path.GetFileNameWithoutExtension(fullPath);
        var author = string.Empty;

        if (format == "EPUB")
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
        else if (format == "FB2")
        {
            try
            {
                var probe = new BookEntry
                {
                    FilePath = fullPath,
                    Format = format,
                    Title = title,
                    Author = author,
                    FileSize = info.Length
                };
                await using var source = await new Fb2FlowAdapter().OpenAsync(probe, cancellationToken);
                if (!string.IsNullOrWhiteSpace(source.Document.Title)) title = source.Document.Title;
                author = source.Document.Author;
            }
            catch
            {
                // Keep filename metadata. Opening the book will surface the parsing error.
            }
        }

        var entry = new BookEntry
        {
            FilePath = fullPath,
            Format = format,
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

    public void Remove(BookEntry book)
    {
        if (Books.Remove(book)) Save();
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
