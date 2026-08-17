using System.Text.Json;
using PageArc.Models;

namespace PageArc.Services;

public sealed class ReadingBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public PageArcReadingBackup CreateBackup(ReadingDataService readingData, IEnumerable<BookEntry> books)
    {
        ArgumentNullException.ThrowIfNull(readingData);
        ArgumentNullException.ThrowIfNull(books);

        var state = readingData.CreateSnapshot();
        return new PageArcReadingBackup
        {
            SchemaVersion = 1,
            ExportedAt = DateTimeOffset.UtcNow,
            Bookmarks = state.Bookmarks,
            Annotations = state.Annotations,
            Progress = books.Select(book => new BookReadingProgressBackup
            {
                BookId = book.Id,
                Title = book.Title,
                Progress = Math.Clamp(book.Progress, 0, 1),
                SectionIndex = Math.Max(0, book.SpineIndex),
                SectionFraction = Math.Clamp(book.SectionFraction, 0, 1),
                LastOpenedAt = book.LastOpenedAt
            }).ToList()
        };
    }

    public async Task ExportAsync(
        string path,
        ReadingDataService readingData,
        IEnumerable<BookEntry> books,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Backup path is required.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var backup = CreateBackup(readingData, books);
        var temp = fullPath + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(backup, JsonOptions), cancellationToken);
        File.Move(temp, fullPath, true);
    }

    public static PageArcReadingBackup Read(string path)
    {
        var backup = JsonSerializer.Deserialize<PageArcReadingBackup>(File.ReadAllText(path), JsonOptions)
                     ?? throw new InvalidDataException("The PageArc backup is empty or invalid.");
        backup.Bookmarks ??= [];
        backup.Annotations ??= [];
        backup.Progress ??= [];
        return backup;
    }
}
