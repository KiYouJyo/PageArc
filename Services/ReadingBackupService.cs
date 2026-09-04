using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PageArc.Models;

namespace PageArc.Services;

public sealed class ReadingBackupService
{
    public const int CurrentSchemaVersion = 2;
    public const string PackageExtension = ".pagearcbackup";
    public const string PackageManifestEntryName = "manifest.json";
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

    public async Task ExportPackageAsync(
        string path,
        ReadingDataService readingData,
        IEnumerable<BookEntry> books,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Backup path is required.", nameof(path));
        ArgumentNullException.ThrowIfNull(readingData);
        ArgumentNullException.ThrowIfNull(books);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var bookList = books.ToList();
        var backup = CreateBackup(readingData, bookList);
        var temp = fullPath + ".tmp";
        try
        {
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, useAsync: true))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false))
            {
                var manifestEntry = archive.CreateEntry(PackageManifestEntryName, CompressionLevel.Optimal);
                using (var writer = new StreamWriter(manifestEntry.Open(), new UTF8Encoding(false)))
                {
                    await writer.WriteAsync(Serialize(backup).AsMemory(), cancellationToken);
                }

                foreach (var book in bookList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(book.FilePath) || !File.Exists(book.FilePath)) continue;

                    var fileName = Path.GetFileName(book.FilePath);
                    if (string.IsNullOrWhiteSpace(fileName)) continue;
                    var entryName = $"books/{SafeArchiveSegment(book.Id)}/{fileName.Replace('\\', '_').Replace('/', '_')}";
                    var bookEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    await using var source = new FileStream(book.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, useAsync: true);
                    using var target = bookEntry.Open();
                    await source.CopyToAsync(target, cancellationToken);
                }
            }

            File.Move(temp, fullPath, true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    public static async Task<string> ComputePackageContentHashAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Backup path is required.", nameof(path));

        var backup = ReadPackage(path);
        var canonical = new PageArcReadingBackup
        {
            SchemaVersion = backup.SchemaVersion,
            ExportedAt = DateTimeOffset.UnixEpoch,
            Books = (backup.Books ?? [])
                .OrderBy(item => item.BookId, StringComparer.Ordinal)
                .ThenBy(item => item.FileName, StringComparer.Ordinal)
                .ToList(),
            Bookmarks = (backup.Bookmarks ?? [])
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToList(),
            Annotations = (backup.Annotations ?? [])
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToList(),
            Progress = (backup.Progress ?? [])
                .OrderBy(item => item.BookId, StringComparer.Ordinal)
                .ToList()
        };

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(Serialize(canonical)));

        var fullPath = Path.GetFullPath(path);
        if (IsPackageArchive(fullPath))
        {
            using var source = File.OpenRead(fullPath);
            using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: false);
            foreach (var entry in archive.Entries
                         .Where(item => item.FullName.StartsWith("books/", StringComparison.Ordinal)
                                        && !item.FullName.EndsWith("/", StringComparison.Ordinal))
                         .OrderBy(item => item.FullName, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                hash.AppendData(Encoding.UTF8.GetBytes(entry.FullName));
                hash.AppendData(new byte[] { 0 });

                using var entryStream = entry.Open();
                var buffer = new byte[1024 * 128];
                while (true)
                {
                    var read = await entryStream.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read == 0) break;
                    hash.AppendData(buffer.AsSpan(0, read));
                }
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static PageArcReadingBackup ReadPackage(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Backup path is required.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        if (!IsPackageArchive(fullPath)) return Read(fullPath);

        using var source = File.OpenRead(fullPath);
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: false);
        var manifest = archive.GetEntry(PackageManifestEntryName)
            ?? throw new InvalidDataException("The PageArc backup package does not contain a manifest.");
        using var reader = new StreamReader(manifest.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Deserialize(reader.ReadToEnd());
    }

    public async Task<int> RestorePackageBooksAsync(
        string path,
        PageArcReadingBackup backup,
        LibraryService library,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(library);
        if (!IsPackageArchive(path)) return 0;

        AppPaths.Ensure();
        var restored = 0;
        using var source = File.OpenRead(Path.GetFullPath(path));
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: false);

        foreach (var identity in backup.Books ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(identity.BookId)) continue;

            var exact = library.Books.FirstOrDefault(book =>
                string.Equals(book.Id, identity.BookId, StringComparison.Ordinal));
            if (exact is not null && !exact.IsMissing && File.Exists(exact.FilePath)) continue;

            BookEntry? fingerprintMatch = null;
            if (!string.IsNullOrWhiteSpace(identity.FileFingerprint))
            {
                var matches = library.Books.Where(book =>
                    !string.IsNullOrWhiteSpace(book.FileFingerprint)
                    && string.Equals(book.FileFingerprint, identity.FileFingerprint, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 1) fingerprintMatch = matches[0];
                if (fingerprintMatch is not null && !fingerprintMatch.IsMissing && File.Exists(fingerprintMatch.FilePath)) continue;
            }

            var prefix = $"books/{SafeArchiveSegment(identity.BookId)}/";
            var entry = archive.Entries.FirstOrDefault(item =>
                item.FullName.StartsWith(prefix, StringComparison.Ordinal)
                && !item.FullName.EndsWith("/", StringComparison.Ordinal));
            if (entry is null) continue;

            var fileName = Path.GetFileName(entry.FullName);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = identity.FileName;
            if (string.IsNullOrWhiteSpace(fileName)) continue;

            var targetDirectory = Path.Combine(AppPaths.ManagedBooksRoot, SafeArchiveSegment(identity.BookId));
            Directory.CreateDirectory(targetDirectory);
            var destination = Path.Combine(targetDirectory, fileName);
            var temp = destination + ".tmp";
            try
            {
                {
                    using var entryStream = entry.Open();
                    await using var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, useAsync: true);
                    await entryStream.CopyToAsync(output, cancellationToken);
                    await output.FlushAsync(cancellationToken);
                }
                File.Move(temp, destination, true);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }

            var targetBook = exact ?? fingerprintMatch;
            if (targetBook is not null)
            {
                var info = new FileInfo(destination);
                targetBook.FilePath = destination;
                targetBook.IsMissing = false;
                targetBook.FileSize = info.Length;
                targetBook.SourceModifiedAt = new DateTimeOffset(info.LastWriteTimeUtc);
                if (!string.IsNullOrWhiteSpace(identity.FileFingerprint)) targetBook.FileFingerprint = identity.FileFingerprint;
                if (string.IsNullOrWhiteSpace(targetBook.Format)) targetBook.Format = identity.Format;
                if (string.IsNullOrWhiteSpace(targetBook.Title)) targetBook.Title = identity.Title;
                if (string.IsNullOrWhiteSpace(targetBook.Author)) targetBook.Author = identity.Author;
                restored++;
                continue;
            }

            var import = await library.ImportDetailedAsync(destination, cancellationToken);
            if (import.Book is not null) restored++;
        }

        library.RefreshFileStates(saveIfChanged: false);
        library.Save();
        return restored;
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

    private static bool IsPackageArchive(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        try
        {
            using var stream = File.OpenRead(Path.GetFullPath(path));
            if (stream.Length < 4) return false;
            Span<byte> signature = stackalloc byte[4];
            if (stream.Read(signature) != signature.Length) return false;
            return signature[0] == 0x50 && signature[1] == 0x4B
                && (signature[2] == 0x03 || signature[2] == 0x05 || signature[2] == 0x07)
                && (signature[3] == 0x04 || signature[3] == 0x06 || signature[3] == 0x08);
        }
        catch
        {
            return false;
        }
    }

    private static string SafeArchiveSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "book";
        var chars = value.Select(ch =>
            char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_').ToArray();
        var safe = new string(chars).Trim('.', ' ');
        return string.IsNullOrWhiteSpace(safe) ? "book" : safe;
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
