using System.Collections.ObjectModel;
using System.Text.Json;
using PageArc.Models;

namespace PageArc.Services;

public sealed class LibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _libraryFile;

    public LibraryService(string? libraryFile = null)
    {
        _libraryFile = string.IsNullOrWhiteSpace(libraryFile) ? AppPaths.LibraryFile : Path.GetFullPath(libraryFile);
    }

    public ObservableCollection<BookEntry> Books { get; } = [];
    public bool DuplicateDetectionEnabled { get; set; } = true;

    public void Load()
    {
        EnsureStorage();
        try
        {
            if (!File.Exists(_libraryFile)) return;
            var items = JsonSerializer.Deserialize<List<BookEntry>>(File.ReadAllText(_libraryFile)) ?? [];
            Books.Clear();
            foreach (var item in items)
            {
                NormalizeLoadedEntry(item);
                Books.Add(item);
            }
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Library load failed; starting with an empty in-memory library.", ex);
            Books.Clear();
        }
    }

    public BookEntry? FindById(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : Books.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));

    public async Task<BookEntry> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = await ImportDetailedAsync(filePath, cancellationToken);
        if (result.Book is not null && result.Disposition is LibraryImportDisposition.Added or LibraryImportDisposition.ExistingPath or LibraryImportDisposition.DuplicateContent)
            return result.Book;

        throw result.Disposition switch
        {
            LibraryImportDisposition.Missing => new FileNotFoundException(result.ErrorMessage ?? "Book file not found.", result.FilePath),
            LibraryImportDisposition.Unsupported => new NotSupportedException(result.ErrorMessage ?? "Unsupported ebook format."),
            _ => new InvalidDataException(result.ErrorMessage ?? "The ebook could not be imported.")
        };
    }

    public async Task<LibraryImportItemResult> ImportDetailedAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(filePath))
            return new LibraryImportItemResult(filePath ?? string.Empty, LibraryImportDisposition.Missing, ErrorMessage: "Book path is empty.");

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            return new LibraryImportItemResult(filePath, LibraryImportDisposition.Failed, ErrorMessage: ex.Message);
        }

        if (!File.Exists(fullPath))
            return new LibraryImportItemResult(fullPath, LibraryImportDisposition.Missing, ErrorMessage: "Book file not found.");

        var info = new FileInfo(fullPath);
        var format = BookFormatRegistry.FormatFromPath(fullPath);
        if (string.IsNullOrWhiteSpace(format))
            return new LibraryImportItemResult(fullPath, LibraryImportDisposition.Unsupported, ErrorMessage: $"Unsupported ebook format: {info.Extension}");

        lock (_gate)
        {
            var existingPath = Books.FirstOrDefault(x => PathsEqual(x.FilePath, fullPath));
            if (existingPath is not null)
            {
                existingPath.IsMissing = false;
                return new LibraryImportItemResult(fullPath, LibraryImportDisposition.ExistingPath, existingPath);
            }
        }

        try
        {
            string? fingerprint = null;
            if (DuplicateDetectionEnabled)
            {
                fingerprint = await LibraryFingerprintService.ComputeAsync(fullPath, cancellationToken);
                lock (_gate)
                {
                    var duplicate = Books.FirstOrDefault(x =>
                        !string.IsNullOrWhiteSpace(x.FileFingerprint)
                        && string.Equals(x.FileFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase));
                    if (duplicate is not null)
                        return new LibraryImportItemResult(fullPath, LibraryImportDisposition.DuplicateContent, duplicate);
                }
            }

            var entry = new BookEntry
            {
                FilePath = fullPath,
                Format = format,
                Title = Path.GetFileNameWithoutExtension(fullPath),
                FileSize = info.Length,
                SourceModifiedAt = info.LastWriteTimeUtc,
                FileFingerprint = fingerprint,
                IsMissing = false
            };

            await EnrichMetadataAsync(entry, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                var racedPath = Books.FirstOrDefault(x => PathsEqual(x.FilePath, fullPath));
                if (racedPath is not null)
                    return new LibraryImportItemResult(fullPath, LibraryImportDisposition.ExistingPath, racedPath);

                if (DuplicateDetectionEnabled && !string.IsNullOrWhiteSpace(entry.FileFingerprint))
                {
                    var racedDuplicate = Books.FirstOrDefault(x =>
                        !string.IsNullOrWhiteSpace(x.FileFingerprint)
                        && string.Equals(x.FileFingerprint, entry.FileFingerprint, StringComparison.OrdinalIgnoreCase));
                    if (racedDuplicate is not null)
                        return new LibraryImportItemResult(fullPath, LibraryImportDisposition.DuplicateContent, racedDuplicate);
                }

                Books.Add(entry);
            }

            Save();
            return new LibraryImportItemResult(fullPath, LibraryImportDisposition.Added, entry);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"Library import failed for '{fullPath}'.", ex);
            return new LibraryImportItemResult(fullPath, LibraryImportDisposition.Failed, ErrorMessage: ex.Message);
        }
    }

    public async Task<LibraryImportSummary> ImportManyAsync(
        IEnumerable<string> filePaths,
        IProgress<LibraryImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        var paths = filePaths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var results = new List<LibraryImportItemResult>(paths.Length);

        for (var index = 0; index < paths.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = paths[index];
            progress?.Report(new LibraryImportProgress(index, paths.Length, path));
            results.Add(await ImportDetailedAsync(path, cancellationToken));
            progress?.Report(new LibraryImportProgress(index + 1, paths.Length, path));
        }

        return new LibraryImportSummary(results);
    }

    public bool RefreshFileStates(bool saveIfChanged = true)
    {
        var changed = false;
        foreach (var book in Books)
        {
            var exists = SafeFileExists(book.FilePath);
            var missing = !exists;
            if (book.IsMissing != missing)
            {
                book.IsMissing = missing;
                changed = true;
            }

            if (!exists) continue;
            try
            {
                var info = new FileInfo(book.FilePath);
                if (book.FileSize != info.Length)
                {
                    book.FileSize = info.Length;
                    changed = true;
                }
                var modified = new DateTimeOffset(info.LastWriteTimeUtc);
                if (book.SourceModifiedAt != modified)
                {
                    book.SourceModifiedAt = modified;
                    changed = true;
                }
            }
            catch
            {
                if (!book.IsMissing)
                {
                    book.IsMissing = true;
                    changed = true;
                }
            }
        }

        if (changed && saveIfChanged) Save();
        return changed;
    }

    public void MarkOpened(BookEntry book)
    {
        book.LastOpenedAt = DateTimeOffset.Now;
        book.IsMissing = !SafeFileExists(book.FilePath);
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
            EnsureStorage();
            var temp = _libraryFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Books.ToList(), JsonOptions));
            File.Move(temp, _libraryFile, true);
        }
    }

    private void NormalizeLoadedEntry(BookEntry item)
    {
        var normalized = BookFormatRegistry.Normalize(item.Format);
        if (string.IsNullOrWhiteSpace(normalized)) normalized = BookFormatRegistry.FormatFromPath(item.FilePath);
        if (!string.IsNullOrWhiteSpace(normalized)) item.Format = normalized;
        item.SectionFraction = Math.Clamp(item.SectionFraction, 0, 1);
        item.Progress = Math.Clamp(item.Progress, 0, 1);
        item.IsMissing = !SafeFileExists(item.FilePath);

        if (!item.IsMissing)
        {
            try
            {
                var info = new FileInfo(item.FilePath);
                if (item.FileSize <= 0) item.FileSize = info.Length;
                item.SourceModifiedAt ??= new DateTimeOffset(info.LastWriteTimeUtc);
            }
            catch
            {
                item.IsMissing = true;
            }
        }
    }

    private static async Task EnrichMetadataAsync(BookEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await BookMetadataService.ReadAsync(entry, cancellationToken);
            if (!string.IsNullOrWhiteSpace(metadata.Title)) entry.Title = metadata.Title;
            if (!string.IsNullOrWhiteSpace(metadata.Author)) entry.Author = metadata.Author;
            entry.Language = metadata.Language;
            entry.Publisher = metadata.Publisher;
            entry.Description = metadata.Description;
            if (!string.IsNullOrWhiteSpace(metadata.CoverPath)) entry.CoverPath = metadata.CoverPath;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StartupDiagnostics.Log($"Metadata enrichment failed for '{entry.FilePath}'.", ex);
        }
    }

    private void EnsureStorage()
    {
        if (string.Equals(_libraryFile, AppPaths.LibraryFile, StringComparison.OrdinalIgnoreCase))
            AppPaths.Ensure();
        var directory = Path.GetDirectoryName(_libraryFile);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
    }

    private static bool SafeFileExists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try { return File.Exists(path); }
        catch { return false; }
    }

    private static bool PathsEqual(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left)) return false;
        try
        {
            return string.Equals(Path.GetFullPath(left), right, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
