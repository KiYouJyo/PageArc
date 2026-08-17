namespace PageArc.Models;

public sealed class PageArcReadingBackup
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ReaderBookmark> Bookmarks { get; set; } = [];
    public List<ReaderAnnotation> Annotations { get; set; } = [];
    public List<BookReadingProgressBackup> Progress { get; set; } = [];
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
