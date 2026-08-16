namespace PageArc.Models;

public sealed class KindleRuntimeBook
{
    public string Format { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string? CoverHref { get; set; }
    public bool FixedLayout { get; set; }
    public string? Direction { get; set; }
    public List<KindleRuntimeSection> Sections { get; set; } = [];
    public List<KindleRuntimeTocItem> Toc { get; set; } = [];
}

public sealed class KindleRuntimeSection
{
    public int OriginalIndex { get; set; }
    public string Id { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool Linear { get; set; } = true;
}

public sealed class KindleRuntimeTocItem
{
    public string Title { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public int? SectionIndex { get; set; }
    public int Depth { get; set; }
}

public sealed class KindleRuntimeSectionContent
{
    public string Html { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
}
