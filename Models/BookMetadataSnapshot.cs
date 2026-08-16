namespace PageArc.Models;

public sealed record BookMetadataSnapshot(
    string Title,
    string Author,
    string Language,
    string Publisher,
    string Description,
    string? CoverPath = null)
{
    public static BookMetadataSnapshot Empty(string title) => new(title, string.Empty, string.Empty, string.Empty, string.Empty);
}
