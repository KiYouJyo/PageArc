namespace PageArc.Models;

public sealed class ReaderBookmark
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string BookId { get; set; } = string.Empty;
    public FlowContentLocator Locator { get; set; } = new(0);
    public string ChapterTitle { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class ReaderAnnotation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string BookId { get; set; } = string.Empty;
    public FlowContentLocator Locator { get; set; } = new(0);
    public string ChapterTitle { get; set; } = string.Empty;
    public string Quote { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string HighlightColor { get; set; } = "yellow";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class ReadingDataState
{
    public List<ReaderBookmark> Bookmarks { get; set; } = [];
    public List<ReaderAnnotation> Annotations { get; set; } = [];
}

public sealed record FlowSearchResult(
    int SectionIndex,
    double Fraction,
    string ChapterTitle,
    string Snippet,
    string MatchText,
    int MatchIndex,
    int MatchLength,
    int OccurrenceInSection = 0);

public sealed record ReaderSearchListItem(FlowSearchResult Result, int Position, int Total)
{
    public string ChapterTitle => Result.ChapterTitle;
    public string Snippet => Result.Snippet;
    public string PositionText => $"{Position} / {Total}";
}

public sealed record ReaderBookmarkListItem(ReaderBookmark Bookmark, double OverallProgress)
{
    public string ChapterTitle => Bookmark.ChapterTitle;
    public string Snippet => Bookmark.Snippet;
    public string PercentText => $"{Math.Round(Math.Clamp(OverallProgress, 0, 1) * 100)}%";
}
