using System.Text.Json.Serialization;

namespace PageArc.Models;

public sealed class BookEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CoverPath { get; set; }
    public string? FileFingerprint { get; set; }
    public long FileSize { get; set; }
    public DateTimeOffset? SourceModifiedAt { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastOpenedAt { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsMissing { get; set; }
    public double Progress { get; set; }
    public int SpineIndex { get; set; }
    public double SectionFraction { get; set; }
    public string? Collection { get; set; }

    [JsonIgnore]
    public string DisplayAuthor => string.IsNullOrWhiteSpace(Author) ? "—" : Author;

    [JsonIgnore]
    public string ProgressText => $"{Math.Round(Progress * 100)}%";

    [JsonIgnore]
    public string FavoriteGlyph => IsFavorite ? "\uE735" : "\uE734";

    [JsonIgnore]
    public string DisplayFileSize => FileSize switch
    {
        >= 1024L * 1024L * 1024L => $"{FileSize / 1024d / 1024d / 1024d:0.00} GB",
        >= 1024L * 1024L => $"{FileSize / 1024d / 1024d:0.00} MB",
        >= 1024L => $"{FileSize / 1024d:0.0} KB",
        _ => $"{FileSize} B"
    };

    [JsonIgnore]
    public string CoverMonogram
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Title)) return "PageArc";
            return Title.Trim();
        }
    }
}

public enum LibraryMode
{
    Library,
    Recent,
    Favorites,
    Collections
}
