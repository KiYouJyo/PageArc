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
    public string? Collection { get; set; }

    [JsonIgnore]
    public string DisplayAuthor => string.IsNullOrWhiteSpace(Author) ? "—" : Author;

    [JsonIgnore]
    public string ProgressText => $"{Math.Round(Progress * 100)}%";

    [JsonIgnore]
    public string CoverMonogram
    {
        get
        {
            var words = Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length switch
            {
                0 => "PA",
                1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
                _ => string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])))
            };
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
