using System.Net;
using System.Text;
using System.Xml.Linq;
using PageArc.Models;

namespace PageArc.Services;

public sealed class Fb2FlowAdapter : IFlowBookAdapter
{
    private static readonly string[] AdapterFormats = ["FB2"];
    public IReadOnlyCollection<string> Formats => AdapterFormats;

    public bool CanOpen(BookEntry book) =>
        string.Equals(BookFormatRegistry.Normalize(book.Format), "FB2", StringComparison.OrdinalIgnoreCase)
        || string.Equals(BookFormatRegistry.FormatFromPath(book.FilePath), "FB2", StringComparison.OrdinalIgnoreCase);

    public async Task<IFlowBookSource> OpenAsync(BookEntry book, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(book.FilePath);
        var xml = await XDocument.LoadAsync(stream, LoadOptions.PreserveWhitespace, cancellationToken);
        return new Source(book, xml);
    }

    private sealed class Source : IFlowBookSource
    {
        private readonly IReadOnlyList<XElement> _sections;
        private readonly IReadOnlyDictionary<string, BinaryResource> _binaries;

        public Source(BookEntry book, XDocument xml)
        {
            var titleInfo = xml.Descendants().FirstOrDefault(x => x.Name.LocalName == "title-info");
            var title = titleInfo?.Elements().FirstOrDefault(x => x.Name.LocalName == "book-title")?.Value.Trim();
            var authors = titleInfo?.Elements()
                .Where(x => x.Name.LocalName == "author")
                .Select(FormatAuthor)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray() ?? [];
            var language = titleInfo?.Elements().FirstOrDefault(x => x.Name.LocalName == "lang")?.Value.Trim();

            _binaries = xml.Descendants()
                .Where(x => x.Name.LocalName == "binary")
                .Select(x => new BinaryResource(
                    (string?)x.Attribute("id") ?? string.Empty,
                    (string?)x.Attribute("content-type") ?? "application/octet-stream",
                    RemoveWhitespace(x.Value)))
                .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Base64))
                .GroupBy(x => x.Id, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);

            var bodies = xml.Root?.Elements().Where(x => x.Name.LocalName == "body").ToArray() ?? [];
            var topLevelSections = bodies.SelectMany(body => body.Elements().Where(x => x.Name.LocalName == "section")).ToArray();
            _sections = topLevelSections.Length > 0 ? topLevelSections : bodies;
            if (_sections.Count == 0)
                throw new InvalidDataException("FB2 does not contain a readable body.");

            var sections = _sections.Select((section, index) =>
            {
                var id = (string?)section.Attribute("id") ?? $"section-{index + 1}";
                return new FlowSection(id, BuildHref(index, id), "application/xhtml+xml", section.ToString(SaveOptions.DisableFormatting).Length);
            }).ToArray();

            var toc = new List<FlowTocItem>();
            for (var i = 0; i < _sections.Count; i++)
                AddTocItems(_sections[i], i, 0, toc);

            Document = new FlowDocument
            {
                Format = "FB2",
                Title = string.IsNullOrWhiteSpace(title) ? book.Title : title,
                Author = authors.Length == 0 ? book.Author : string.Join(", ", authors),
                Language = language,
                Sections = sections,
                Toc = toc
            };
        }

        public FlowDocument Document { get; }

        public Task<FlowSectionContent> LoadSectionAsync(int sectionIndex, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sectionIndex < 0 || sectionIndex >= _sections.Count)
                throw new ArgumentOutOfRangeException(nameof(sectionIndex));

            var section = _sections[sectionIndex];
            var builder = new StringBuilder();
            builder.Append("<!doctype html><html><head><meta charset=\"utf-8\">");
            builder.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            builder.Append("<style>body{margin:0;padding:0;font:inherit;color:inherit;background:transparent}img{max-width:100%;height:auto}blockquote{margin-inline:1.5em}table{border-collapse:collapse;max-width:100%}td,th{padding:.2em .35em}</style>");
            builder.Append("</head><body>");
            RenderElement(section, builder, _binaries);
            builder.Append("</body></html>");
            var html = builder.ToString();
            var text = EpubWebRenderer.ExtractReadableText(html);
            return Task.FromResult(new FlowSectionContent(html, text, BuildHref(sectionIndex, (string?)section.Attribute("id"))));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static void AddTocItems(XElement section, int sectionIndex, int depth, ICollection<FlowTocItem> toc)
        {
            var titleElement = section.Elements().FirstOrDefault(x => x.Name.LocalName == "title");
            var title = titleElement is null ? string.Empty : NormalizeText(titleElement.Value);
            var id = (string?)section.Attribute("id");
            if (!string.IsNullOrWhiteSpace(title))
                toc.Add(new FlowTocItem(title, BuildHref(sectionIndex, id), sectionIndex, depth));

            foreach (var child in section.Elements().Where(x => x.Name.LocalName == "section"))
                AddTocItems(child, sectionIndex, depth + 1, toc);
        }

        private static void RenderElement(XElement element, StringBuilder builder, IReadOnlyDictionary<string, BinaryResource> binaries)
        {
            var name = element.Name.LocalName;
            switch (name)
            {
                case "title":
                    builder.Append("<h2>").Append(WebUtility.HtmlEncode(NormalizeText(element.Value))).Append("</h2>");
                    return;
                case "subtitle": RenderContainer("h3", element, builder, binaries); return;
                case "section": RenderContainer("section", element, builder, binaries, element.Attribute("id")?.Value); return;
                case "p": RenderContainer("p", element, builder, binaries); return;
                case "emphasis": RenderContainer("em", element, builder, binaries); return;
                case "strong": RenderContainer("strong", element, builder, binaries); return;
                case "strikethrough": RenderContainer("s", element, builder, binaries); return;
                case "code": RenderContainer("code", element, builder, binaries); return;
                case "sub": RenderContainer("sub", element, builder, binaries); return;
                case "sup": RenderContainer("sup", element, builder, binaries); return;
                case "cite":
                case "epigraph": RenderContainer("blockquote", element, builder, binaries); return;
                case "poem": RenderContainer("div", element, builder, binaries); return;
                case "stanza": RenderContainer("div", element, builder, binaries); return;
                case "v": RenderContainer("p", element, builder, binaries); return;
                case "text-author": RenderContainer("p", element, builder, binaries); return;
                case "table": RenderContainer("table", element, builder, binaries); return;
                case "tr": RenderContainer("tr", element, builder, binaries); return;
                case "td": RenderContainer("td", element, builder, binaries); return;
                case "th": RenderContainer("th", element, builder, binaries); return;
                case "empty-line": builder.Append("<br>"); return;
                case "image": RenderImage(element, builder, binaries); return;
                case "a": RenderLink(element, builder, binaries); return;
                default:
                    foreach (var node in element.Nodes()) RenderNode(node, builder, binaries);
                    return;
            }
        }

        private static void RenderContainer(string tag, XElement element, StringBuilder builder, IReadOnlyDictionary<string, BinaryResource> binaries, string? id = null)
        {
            builder.Append('<').Append(tag);
            if (!string.IsNullOrWhiteSpace(id)) builder.Append(" id=\"").Append(WebUtility.HtmlEncode(id)).Append('\"');
            builder.Append('>');
            foreach (var node in element.Nodes()) RenderNode(node, builder, binaries);
            builder.Append("</").Append(tag).Append('>');
        }

        private static void RenderNode(XNode node, StringBuilder builder, IReadOnlyDictionary<string, BinaryResource> binaries)
        {
            if (node is XText text) builder.Append(WebUtility.HtmlEncode(text.Value));
            else if (node is XElement element) RenderElement(element, builder, binaries);
        }

        private static void RenderImage(XElement element, StringBuilder builder, IReadOnlyDictionary<string, BinaryResource> binaries)
        {
            var href = element.Attributes().FirstOrDefault(x => x.Name.LocalName == "href")?.Value?.Trim();
            var id = href?.TrimStart('#');
            if (string.IsNullOrWhiteSpace(id) || !binaries.TryGetValue(id, out var binary)) return;
            builder.Append("<img alt=\"\" src=\"data:")
                .Append(WebUtility.HtmlEncode(binary.MediaType))
                .Append(";base64,")
                .Append(binary.Base64)
                .Append("\">");
        }

        private static void RenderLink(XElement element, StringBuilder builder, IReadOnlyDictionary<string, BinaryResource> binaries)
        {
            var href = element.Attributes().FirstOrDefault(x => x.Name.LocalName == "href")?.Value?.Trim();
            builder.Append("<a");
            if (!string.IsNullOrWhiteSpace(href)) builder.Append(" href=\"").Append(WebUtility.HtmlEncode(href)).Append('\"');
            builder.Append('>');
            foreach (var node in element.Nodes()) RenderNode(node, builder, binaries);
            builder.Append("</a>");
        }

        private static string FormatAuthor(XElement author)
        {
            var parts = new[] { "first-name", "middle-name", "last-name", "nickname" }
                .Select(name => author.Elements().FirstOrDefault(x => x.Name.LocalName == name)?.Value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value));
            return string.Join(" ", parts!);
        }

        private static string BuildHref(int index, string? id) =>
            $"fb2://section/{index}" + (string.IsNullOrWhiteSpace(id) ? string.Empty : $"#{Uri.EscapeDataString(id)}");

        private static string RemoveWhitespace(string value) => new(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
        private static string NormalizeText(string value) => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        private sealed record BinaryResource(string Id, string MediaType, string Base64);
    }
}
