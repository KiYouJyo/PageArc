using PageArc.Models;

namespace PageArc.Services;

public sealed class MobiFlowAdapter : IFlowBookAdapter
{
    private static readonly string[] AdapterFormats = ["MOBI", "AZW3"];
    private readonly IKindleParserRuntime _runtime;

    public MobiFlowAdapter(IKindleParserRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public IReadOnlyCollection<string> Formats => AdapterFormats;

    public bool CanOpen(BookEntry book)
    {
        ArgumentNullException.ThrowIfNull(book);
        var format = BookFormatRegistry.Normalize(book.Format);
        if (string.IsNullOrWhiteSpace(format)) format = BookFormatRegistry.FormatFromPath(book.FilePath);
        return AdapterFormats.Contains(format, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IFlowBookSource> OpenAsync(BookEntry book, CancellationToken cancellationToken = default)
    {
        if (!CanOpen(book))
            throw new NotSupportedException($"The Kindle flow adapter cannot open {book.Format}.");

        var parsed = await _runtime.OpenAsync(book, cancellationToken);
        if (parsed.Sections.Count == 0)
            throw new InvalidDataException("The Kindle ebook does not expose any readable sections.");
        return new Source(_runtime, parsed);
    }

    private sealed class Source : IFlowBookSource
    {
        private readonly IKindleParserRuntime _runtime;
        private bool _disposed;

        public Source(IKindleParserRuntime runtime, KindleRuntimeBook parsed)
        {
            _runtime = runtime;
            Document = new FlowDocument
            {
                Format = BookFormatRegistry.Normalize(parsed.Format),
                Title = parsed.Title,
                Author = parsed.Author,
                Language = parsed.Language,
                CoverHref = parsed.CoverHref,
                Sections = parsed.Sections.Select((section, index) => new FlowSection(
                    string.IsNullOrWhiteSpace(section.Id) ? $"kindle-{index}" : section.Id,
                    $"kindle://section/{index}",
                    "application/xhtml+xml",
                    section.Size,
                    section.Linear)).ToArray(),
                Toc = parsed.Toc.Select(item => new FlowTocItem(
                    item.Title,
                    item.Href,
                    item.SectionIndex,
                    item.Depth)).ToArray()
            };
        }

        public FlowDocument Document { get; }

        public async Task<FlowSectionContent> LoadSectionAsync(int sectionIndex, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (sectionIndex < 0 || sectionIndex >= Document.Sections.Count)
                throw new ArgumentOutOfRangeException(nameof(sectionIndex));

            var content = await _runtime.LoadSectionAsync(sectionIndex, cancellationToken);
            return new FlowSectionContent(content.Html, content.PlainText, $"kindle://section/{sectionIndex}");
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            await _runtime.CloseAsync();
            await _runtime.DisposeAsync();
        }
    }
}
