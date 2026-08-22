namespace PageArc.Models;

public enum ReadingBackupRestoreMode
{
    Merge,
    Replace
}

public sealed class PageArcReadingBackup
{
    public int SchemaVersion { get; set; } = 2;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ReadingBackupBookIdentity> Books { get; set; } = [];
    public List<ReaderBookmark> Bookmarks { get; set; } = [];
    public List<ReaderAnnotation> Annotations { get; set; } = [];
    public List<BookReadingProgressBackup> Progress { get; set; } = [];
}

public sealed class ReadingBackupBookIdentity
{
    public string BookId { get; set; } = string.Empty;
    public string? FileFingerprint { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public sealed class BookReadingProgressBackup
{
    public string BookId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double Progress { get; set; }
    public int SectionIndex { get; set; }
    public double SectionFraction { get; set; }
    public DateTimeOffset? LastOpenedAt { get; set; }
}

public sealed record ReadingBackupRestoreResult(
    int MatchedBooks,
    int UnmatchedBooks,
    int RestoredBookmarks,
    int RestoredAnnotations,
    int RestoredProgress,
    int SkippedItems);
