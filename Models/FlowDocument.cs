namespace PageArc.Models;

public sealed record FlowSection(
    string Id,
    string Href,
    string MediaType,
    long Size = 0,
    bool Linear = true);

public sealed record FlowTocItem(
    string Title,
    string Href,
    int? SectionIndex = null,
    int Depth = 0);

public sealed record FlowContentLocator(
    int SectionIndex,
    double Fraction = 0,
    string? Fragment = null,
    string? TextQuote = null)
{
    public FlowContentLocator Clamp(int sectionCount)
    {
        if (sectionCount <= 0) return this with { SectionIndex = 0, Fraction = 0 };
        return this with
        {
            SectionIndex = Math.Clamp(SectionIndex, 0, sectionCount - 1),
            Fraction = Math.Clamp(Fraction, 0, 1)
        };
    }
}

public sealed record FlowSectionContent(
    string Html,
    string PlainText,
    string? BaseHref = null);

public sealed class FlowDocument
{
    public required string Format { get; init; }
    public required string Title { get; init; }
    public string Author { get; init; } = string.Empty;
    public string? Language { get; init; }
    public string? CoverHref { get; init; }
    public string? CacheRoot { get; init; }
    public IReadOnlyList<FlowSection> Sections { get; init; } = [];
    public IReadOnlyList<FlowTocItem> Toc { get; init; } = [];
}
