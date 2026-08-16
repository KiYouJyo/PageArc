using System.Text.Json.Serialization;

namespace PageArc.Models;

public sealed class BookEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastOpenedAt { get; set; }
    public bool IsFavorite { get; set; }
    public double Progress { get; set; }
    public int SpineIndex { get; set; }
    public double SectionFraction { get; set; }
    public string? Collection { get; set; }

    [JsonIgnore]
    public string DisplayAuthor => string.IsNullOrWhiteSpace(Author) ? "—" : Author;

    [JsonIgnore]
    public string ProgressText => $"{Math.Round(Progress * 100)}%";

    [JsonIgnore]
    public string FavoriteGlyph => IsFavorite ? "★" : "☆";

    [JsonIgnore]
    public string CoverMonogram
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Title)) return "PA";
            var chars = Title.Where(char.IsLetterOrDigit).Take(2).ToArray();
            if (chars.Length == 0) return string.IsNullOrWhiteSpace(Format) ? "PA" : Format[..Math.Min(2, Format.Length)].ToUpperInvariant();
            return new string(chars).ToUpperInvariant();
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
