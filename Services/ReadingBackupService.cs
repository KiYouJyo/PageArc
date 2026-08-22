using System.Text.Json;
using PageArc.Models;

namespace PageArc.Services;

public sealed class ReadingBackupService
{
    public const int CurrentSchemaVersion = 2;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public PageArcReadingBackup CreateBackup(ReadingDataService readingData, IEnumerable<BookEntry> books)
    {
        ArgumentNullException.ThrowIfNull(readingData);
        ArgumentNullException.ThrowIfNull(books);
        var bookList = books.ToList();
        var state = readingData.CreateSnapshot();
        return new PageArcReadingBackup
        {
            SchemaVersion = CurrentSchemaVersion,
            ExportedAt = DateTimeOffset.UtcNow,
            Books = bookList.Select(book => new ReadingBackupBookIdentity
            {
                BookId = book.Id,
                FileFingerprint = book.FileFingerprint,
                Title = book.Title,
                Author = book.Author,
                Format = BookFormatRegistry.Normalize(book.Format),
                FileName = Path.GetFileName(book.FilePath)
            }).ToList(),
            Bookmarks = state.Bookmarks,
            Annotations = state.Annotations,
            Progress = bookList.Select(book => new BookReadingProgressBackup
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
        await File.WriteAllTextAsync(temp, Serialize(backup), cancellationToken);
        File.Move(temp, fullPath, true);
    }

    public static string Serialize(PageArcReadingBackup backup)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ValidateSchema(backup.SchemaVersion);
        return JsonSerializer.Serialize(backup, JsonOptions);
    }

    public static PageArcReadingBackup Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("The PageArc backup is empty or invalid.");
        var backup = JsonSerializer.Deserialize<PageArcReadingBackup>(json, JsonOptions)
                     ?? throw new InvalidDataException("The PageArc backup is empty or invalid.");
        ValidateSchema(backup.SchemaVersion);
        backup.Books ??= [];
        backup.Bookmarks ??= [];
        backup.Annotations ??= [];
        backup.Progress ??= [];
        return backup;
    }

    public static PageArcReadingBackup Merge(PageArcReadingBackup local, PageArcReadingBackup remote)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);
        ValidateSchema(local.SchemaVersion);
        ValidateSchema(remote.SchemaVersion);

        return new PageArcReadingBackup
        {
            SchemaVersion = CurrentSchemaVersion,
            ExportedAt = DateTimeOffset.UtcNow,
            Books = MergeByKey(local.Books, remote.Books, item => item.BookId, (_, incoming) => incoming),
            Bookmarks = MergeByKey(local.Bookmarks, remote.Bookmarks, item => item.Id,
                (existing, incoming) => incoming.CreatedAt >= existing.CreatedAt ? incoming : existing),
            Annotations = MergeByKey(local.Annotations, remote.Annotations, item => item.Id,
                (existing, incoming) => incoming.UpdatedAt >= existing.UpdatedAt ? incoming : existing),
            Progress = MergeByKey(local.Progress, remote.Progress, item => item.BookId,
                (existing, incoming) => (incoming.LastOpenedAt ?? DateTimeOffset.MinValue) >= (existing.LastOpenedAt ?? DateTimeOffset.MinValue)
                    ? incoming
                    : existing)
        };
    }

    public ReadingBackupRestoreResult Restore(
        PageArcReadingBackup backup,
        ReadingDataService readingData,
        IEnumerable<BookEntry> books,
        ReadingBackupRestoreMode mode)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(readingData);
        ArgumentNullException.ThrowIfNull(books);
        ValidateSchema(backup.SchemaVersion);
        var bookList = books.ToList();

        var identities = (backup.Books ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.BookId))
            .GroupBy(item => item.BookId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var legacyTitles = (backup.Progress ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.BookId) && !string.IsNullOrWhiteSpace(item.Title))
            .GroupBy(item => item.BookId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Title, StringComparer.Ordinal);

        var sourceIds = (backup.Bookmarks ?? []).Select(item => item.BookId)
            .Concat((backup.Annotations ?? []).Select(item => item.BookId))
            .Concat((backup.Progress ?? []).Select(item => item.BookId))
            .Concat(identities.Keys)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var mapping = new Dictionary<string, BookEntry?>(StringComparer.Ordinal);
        foreach (var sourceId in sourceIds)
            mapping[sourceId] = ResolveBook(sourceId, identities, legacyTitles, bookList);

        var restoredState = new ReadingDataState();
        var skipped = 0;
        foreach (var bookmark in backup.Bookmarks ?? [])
        {
            if (!mapping.TryGetValue(bookmark.BookId, out var book) || book is null) { skipped++; continue; }
            restoredState.Bookmarks.Add(new ReaderBookmark
            {
                Id = bookmark.Id,
                BookId = book.Id,
                Locator = bookmark.Locator,
                ChapterTitle = bookmark.ChapterTitle,
                Snippet = bookmark.Snippet,
                CreatedAt = bookmark.CreatedAt
            });
        }
        foreach (var annotation in backup.Annotations ?? [])
        {
            if (!mapping.TryGetValue(annotation.BookId, out var book) || book is null) { skipped++; continue; }
            restoredState.Annotations.Add(new ReaderAnnotation
            {
                Id = annotation.Id,
                BookId = book.Id,
                Locator = annotation.Locator,
                ChapterTitle = annotation.ChapterTitle,
                Quote = annotation.Quote,
                Note = annotation.Note,
                HighlightColor = annotation.HighlightColor,
                CreatedAt = annotation.CreatedAt,
                UpdatedAt = annotation.UpdatedAt
            });
        }
        readingData.RestoreSnapshot(restoredState, mode);

        var restoredProgress = 0;
        foreach (var progress in backup.Progress ?? [])
        {
            if (!mapping.TryGetValue(progress.BookId, out var book) || book is null) { skipped++; continue; }
            book.Progress = Math.Clamp(progress.Progress, 0, 1);
            book.SpineIndex = Math.Max(0, progress.SectionIndex);
            book.SectionFraction = Math.Clamp(progress.SectionFraction, 0, 1);
            book.LastOpenedAt = progress.LastOpenedAt;
            restoredProgress++;
        }

        return new ReadingBackupRestoreResult(
            mapping.Values.Where(book => book is not null).Select(book => book!.Id).Distinct(StringComparer.Ordinal).Count(),
            mapping.Values.Count(book => book is null),
            restoredState.Bookmarks.Count,
            restoredState.Annotations.Count,
            restoredProgress,
            skipped);
    }

    public static PageArcReadingBackup Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Backup path is required.", nameof(path));
        return Deserialize(File.ReadAllText(path));
    }

    private static List<T> MergeByKey<T>(
        IEnumerable<T>? local,
        IEnumerable<T>? remote,
        Func<T, string> keySelector,
        Func<T, T, T> resolve)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in (local ?? []).Concat(remote ?? []))
        {
            var key = keySelector(item);
            if (string.IsNullOrWhiteSpace(key)) key = Guid.NewGuid().ToString("N");
            result[key] = result.TryGetValue(key, out var existing) ? resolve(existing, item) : item;
        }
        return result.Values.ToList();
    }

    private static void ValidateSchema(int version)
    {
        if (version is < 1 or > CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported PageArc reading backup schema: {version}.");
    }

    private static BookEntry? ResolveBook(
        string sourceBookId,
        IReadOnlyDictionary<string, ReadingBackupBookIdentity> identities,
        IReadOnlyDictionary<string, string> legacyTitles,
        IReadOnlyList<BookEntry> books)
    {
        var exact = books.FirstOrDefault(book => string.Equals(book.Id, sourceBookId, StringComparison.Ordinal));
        if (exact is not null) return exact;

        if (identities.TryGetValue(sourceBookId, out var identity))
        {
            if (!string.IsNullOrWhiteSpace(identity.FileFingerprint))
            {
                var fingerprintMatches = books.Where(book =>
                    !string.IsNullOrWhiteSpace(book.FileFingerprint)
                    && string.Equals(book.FileFingerprint, identity.FileFingerprint, StringComparison.OrdinalIgnoreCase)).ToList();
                if (fingerprintMatches.Count == 1) return fingerprintMatches[0];
            }

            var identityMatches = books.Where(book =>
                SameText(book.Title, identity.Title)
                && SameText(book.Author, identity.Author)
                && SameText(BookFormatRegistry.Normalize(book.Format), BookFormatRegistry.Normalize(identity.Format))).ToList();
            if (identityMatches.Count == 1) return identityMatches[0];
        }

        if (legacyTitles.TryGetValue(sourceBookId, out var title))
        {
            var titleMatches = books.Where(book => SameText(book.Title, title)).ToList();
            if (titleMatches.Count == 1) return titleMatches[0];
        }
        return null;
    }

    private static bool SameText(string? left, string? right) =>
        string.Equals((left ?? string.Empty).Trim(), (right ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
}
