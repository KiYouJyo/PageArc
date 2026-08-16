using PageArc.Models;

namespace PageArc.Services;

public sealed class FlowSearchService
{
    public async Task<IReadOnlyList<FlowSearchResult>> SearchAsync(
        IFlowBookSource source,
        string query,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        query = query?.Trim() ?? string.Empty;
        if (query.Length == 0 || maxResults <= 0) return [];

        var results = new List<FlowSearchResult>(Math.Min(maxResults, 32));
        var document = source.Document;
        for (var sectionIndex = 0; sectionIndex < document.Sections.Count && results.Count < maxResults; sectionIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await source.LoadSectionAsync(sectionIndex, cancellationToken);
            var text = content.PlainText ?? string.Empty;
            if (text.Length == 0) continue;

            var chapterTitle = ResolveChapterTitle(document, sectionIndex);
            var searchFrom = 0;
            var occurrence = 0;
            while (searchFrom < text.Length && results.Count < maxResults)
            {
                var match = text.IndexOf(query, searchFrom, StringComparison.CurrentCultureIgnoreCase);
                if (match < 0) break;
                var length = Math.Min(query.Length, text.Length - match);
                results.Add(new FlowSearchResult(
                    sectionIndex,
                    Math.Clamp(match / (double)Math.Max(1, text.Length), 0, 1),
                    chapterTitle,
                    BuildSnippet(text, match, length),
                    text.Substring(match, length),
                    match,
                    length,
                    occurrence));
                occurrence++;
                searchFrom = Math.Max(match + Math.Max(1, length), searchFrom + 1);
            }
        }

        return results;
    }

    public static string ResolveChapterTitle(FlowDocument document, int sectionIndex)
    {
        var title = document.Toc
            .Where(item => item.SectionIndex is int section && section <= sectionIndex)
            .OrderBy(item => item.SectionIndex)
            .ThenBy(item => item.Depth)
            .LastOrDefault()?.Title;
        return string.IsNullOrWhiteSpace(title) ? $"Chapter {sectionIndex + 1}" : title;
    }

    public static string BuildSnippet(string text, int matchIndex, int matchLength, int radius = 42)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        matchIndex = Math.Clamp(matchIndex, 0, Math.Max(0, text.Length - 1));
        matchLength = Math.Clamp(matchLength, 0, text.Length - matchIndex);
        var start = Math.Max(0, matchIndex - Math.Max(8, radius));
        var end = Math.Min(text.Length, matchIndex + matchLength + Math.Max(8, radius));
        var snippet = string.Join(" ", text[start..end].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (start > 0) snippet = "…" + snippet;
        if (end < text.Length) snippet += "…";
        return snippet;
    }
}
