namespace PageArc.Models;

public sealed record EpubSpineItem(string Id, string RelativePath, string MediaType);
public sealed record EpubTocItem(string Title, string Href);

public sealed class EpubDocument
{
    public required string Title { get; init; }
    public string Author { get; init; } = string.Empty;
    public required string ExtractionRoot { get; init; }
    public required string PackagePath { get; init; }
    public IReadOnlyList<EpubSpineItem> Spine { get; init; } = [];
    public IReadOnlyList<EpubTocItem> Toc { get; init; } = [];
}
