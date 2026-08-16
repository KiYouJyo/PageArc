using PageArc.Models;

namespace PageArc.Services;

public sealed class EpubFlowAdapter : IFlowBookAdapter
{
    private static readonly string[] AdapterFormats = ["EPUB"];
    public IReadOnlyCollection<string> Formats => AdapterFormats;

    public bool CanOpen(BookEntry book) =>
        string.Equals(BookFormatRegistry.Normalize(book.Format), "EPUB", StringComparison.OrdinalIgnoreCase)
        || string.Equals(BookFormatRegistry.FormatFromPath(book.FilePath), "EPUB", StringComparison.OrdinalIgnoreCase);

    public async Task<IFlowBookSource> OpenAsync(BookEntry book, CancellationToken cancellationToken = default)
    {
        var epub = await EpubParser.OpenAsync(book, cancellationToken);
        return new Source(epub);
    }

    private sealed class Source : IFlowBookSource
    {
        private readonly EpubDocument _epub;

        public Source(EpubDocument epub)
        {
            _epub = epub;
            var sections = epub.Spine
                .Select(item => new FlowSection(item.Id, item.RelativePath, item.MediaType))
                .ToArray();
            var toc = epub.Toc
                .Select(item => new FlowTocItem(item.Title, item.Href, ResolveSectionIndex(epub, item.Href)))
                .ToArray();

            Document = new FlowDocument
            {
                Format = "EPUB",
                Title = epub.Title,
                Author = epub.Author,
                CacheRoot = epub.ExtractionRoot,
                Sections = sections,
                Toc = toc
            };
        }

        public FlowDocument Document { get; }

        public async Task<FlowSectionContent> LoadSectionAsync(int sectionIndex, CancellationToken cancellationToken = default)
        {
            if (sectionIndex < 0 || sectionIndex >= _epub.Spine.Count)
                throw new ArgumentOutOfRangeException(nameof(sectionIndex));

            var chapter = await EpubWebRenderer.PrepareAsync(_epub, sectionIndex, cancellationToken);
            return new FlowSectionContent(chapter.Html, chapter.PlainText, chapter.WebPath);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static int? ResolveSectionIndex(EpubDocument document, string href)
        {
            var hash = href.IndexOf('#');
            var path = hash >= 0 ? href[..hash] : href;
            var normalized = EpubPath.Normalize(path);
            var index = document.Spine.ToList().FindIndex(item =>
                string.Equals(EpubPath.Normalize(item.RelativePath), normalized, StringComparison.OrdinalIgnoreCase));
            return index >= 0 ? index : null;
        }
    }
}
