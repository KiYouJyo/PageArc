using System.Text.Json;
using PageArc.Models;

namespace PageArc.Services;

public sealed class ReadingDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _filePath;
    private ReadingDataState _state = new();

    public ReadingDataService(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? AppPaths.ReadingDataFile : Path.GetFullPath(filePath);
    }

    public void Load()
    {
        lock (_gate)
        {
            EnsureStorageDirectory();
            try
            {
                if (!File.Exists(_filePath))
                {
                    _state = new ReadingDataState();
                    return;
                }

                _state = JsonSerializer.Deserialize<ReadingDataState>(File.ReadAllText(_filePath), JsonOptions)
                         ?? new ReadingDataState();
                _state.Bookmarks ??= [];
                _state.Annotations ??= [];
            }
            catch
            {
                _state = new ReadingDataState();
            }
        }
    }

    public IReadOnlyList<ReaderBookmark> GetBookmarks(string bookId)
    {
        lock (_gate)
        {
            return _state.Bookmarks
                .Where(x => string.Equals(x.BookId, bookId, StringComparison.Ordinal))
                .OrderBy(x => x.Locator.SectionIndex)
                .ThenBy(x => x.Locator.Fraction)
                .Select(Clone)
                .ToArray();
        }
    }

    public IReadOnlyList<ReaderAnnotation> GetAnnotations(string bookId)
    {
        lock (_gate)
        {
            return _state.Annotations
                .Where(x => string.Equals(x.BookId, bookId, StringComparison.Ordinal))
                .OrderBy(x => x.Locator.SectionIndex)
                .ThenBy(x => x.Locator.Fraction)
                .Select(Clone)
                .ToArray();
        }
    }

    public ReadingDataState CreateSnapshot()
    {
        lock (_gate)
        {
            return new ReadingDataState
            {
                Bookmarks = _state.Bookmarks.Select(Clone).ToList(),
                Annotations = _state.Annotations.Select(Clone).ToList()
            };
        }
    }

    public bool HasBookmark(string bookId, FlowContentLocator locator, double tolerance = 0.01)
    {
        lock (_gate)
        {
            return FindBookmark(bookId, locator, tolerance) is not null;
        }
    }

    public ReaderBookmark? ToggleBookmark(
        string bookId,
        FlowContentLocator locator,
        string chapterTitle,
        string snippet,
        double tolerance = 0.01)
    {
        lock (_gate)
        {
            var existing = FindBookmark(bookId, locator, tolerance);
            if (existing is not null)
            {
                _state.Bookmarks.Remove(existing);
                SaveLocked();
                return null;
            }

            var bookmark = new ReaderBookmark
            {
                BookId = bookId,
                Locator = locator,
                ChapterTitle = chapterTitle,
                Snippet = snippet,
                CreatedAt = DateTimeOffset.Now
            };
            _state.Bookmarks.Add(bookmark);
            SaveLocked();
            return Clone(bookmark);
        }
    }

    public bool RemoveBookmark(string bookmarkId)
    {
        lock (_gate)
        {
            var removed = _state.Bookmarks.RemoveAll(x => string.Equals(x.Id, bookmarkId, StringComparison.Ordinal)) > 0;
            if (removed) SaveLocked();
            return removed;
        }
    }

    public ReaderAnnotation SaveAnnotation(ReaderAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        lock (_gate)
        {
            var existing = _state.Annotations.FirstOrDefault(x => string.Equals(x.Id, annotation.Id, StringComparison.Ordinal));
            var now = DateTimeOffset.Now;
            if (existing is null)
            {
                var copy = Clone(annotation);
                if (string.IsNullOrWhiteSpace(copy.Id)) copy.Id = Guid.NewGuid().ToString("N");
                copy.CreatedAt = annotation.CreatedAt == default ? now : annotation.CreatedAt;
                copy.UpdatedAt = now;
                _state.Annotations.Add(copy);
                SaveLocked();
                return Clone(copy);
            }

            existing.BookId = annotation.BookId;
            existing.Locator = annotation.Locator;
            existing.ChapterTitle = annotation.ChapterTitle;
            existing.Quote = annotation.Quote;
            existing.Note = annotation.Note;
            existing.HighlightColor = annotation.HighlightColor;
            existing.UpdatedAt = now;
            SaveLocked();
            return Clone(existing);
        }
    }

    public bool RemoveAnnotation(string annotationId)
    {
        lock (_gate)
        {
            var removed = _state.Annotations.RemoveAll(x => string.Equals(x.Id, annotationId, StringComparison.Ordinal)) > 0;
            if (removed) SaveLocked();
            return removed;
        }
    }

    public void RemoveBookData(string bookId)
    {
        lock (_gate)
        {
            var removed = _state.Bookmarks.RemoveAll(x => string.Equals(x.BookId, bookId, StringComparison.Ordinal));
            removed += _state.Annotations.RemoveAll(x => string.Equals(x.BookId, bookId, StringComparison.Ordinal));
            if (removed > 0) SaveLocked();
        }
    }

    private ReaderBookmark? FindBookmark(string bookId, FlowContentLocator locator, double tolerance) =>
        _state.Bookmarks.FirstOrDefault(x =>
            string.Equals(x.BookId, bookId, StringComparison.Ordinal)
            && x.Locator.SectionIndex == locator.SectionIndex
            && Math.Abs(x.Locator.Fraction - locator.Fraction) <= Math.Max(0, tolerance));

    private void SaveLocked()
    {
        EnsureStorageDirectory();
        var temp = _filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_state, JsonOptions));
        File.Move(temp, _filePath, true);
    }

    private void EnsureStorageDirectory()
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(_filePath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
    }

    private static ReaderBookmark Clone(ReaderBookmark value) => new()
    {
        Id = value.Id,
        BookId = value.BookId,
        Locator = value.Locator,
        ChapterTitle = value.ChapterTitle,
        Snippet = value.Snippet,
        CreatedAt = value.CreatedAt
    };

    private static ReaderAnnotation Clone(ReaderAnnotation value) => new()
    {
        Id = value.Id,
        BookId = value.BookId,
        Locator = value.Locator,
        ChapterTitle = value.ChapterTitle,
        Quote = value.Quote,
        Note = value.Note,
        HighlightColor = value.HighlightColor,
        CreatedAt = value.CreatedAt,
        UpdatedAt = value.UpdatedAt
    };
}
