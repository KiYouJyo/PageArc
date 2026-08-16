using System.Text.Json.Serialization;

namespace PageArc.Models;

public sealed class CategoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    [JsonIgnore]
    public string Monogram
    {
        get
        {
            var text = Name.Trim();
            if (text.Length == 0) return "#";
            return text.EnumerateRunes().First().ToString().ToUpperInvariant();
        }
    }
}
