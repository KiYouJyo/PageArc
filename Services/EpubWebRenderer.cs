using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using PageArc.Models;

namespace PageArc.Services;

public sealed record EpubRenderChapter(string WebPath, string Html, string PlainText);

public static class EpubWebRenderer
{
    public static async Task<EpubRenderChapter> PrepareAsync(
        EpubDocument document,
        int spineIndex,
        CancellationToken cancellationToken = default)
    {
        if (spineIndex < 0 || spineIndex >= document.Spine.Count)
            throw new ArgumentOutOfRangeException(nameof(spineIndex));

        var item = document.Spine[spineIndex];
        var sourcePath = ResolveSafePath(document.ExtractionRoot, item.RelativePath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("EPUB chapter file is missing from the extracted book cache.", sourcePath);

        await using var stream = File.OpenRead(sourcePath);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var source = await reader.ReadToEndAsync(cancellationToken);
        var baseHref = BuildBaseHref(item.RelativePath);
        var html = NormalizeForWebView(source, baseHref);
        var plainText = ExtractReadableText(source);

        var renderDirectory = Path.Combine(document.ExtractionRoot, "__pagearc");
        Directory.CreateDirectory(renderDirectory);
        var renderFileName = $"spine-{spineIndex:D4}.html";
        var renderPath = Path.Combine(renderDirectory, renderFileName);
        await File.WriteAllTextAsync(renderPath, html, new UTF8Encoding(false), cancellationToken);
        return new EpubRenderChapter(EpubPath.ToWebPath($"__pagearc/{renderFileName}"), html, plainText);
    }

    public static int ResolveInitialSpineIndex(EpubDocument document, int savedIndex, double progress)
    {
        if (document.Spine.Count == 0) return 0;
        var saved = Math.Clamp(savedIndex, 0, document.Spine.Count - 1);
        if (saved > 0 || progress > 0.001) return saved;

        foreach (var tocItem in document.Toc)
        {
            var tocPath = EpubPath.Normalize(tocItem.Href);
            var index = document.Spine.ToList().FindIndex(item =>
                string.Equals(EpubPath.Normalize(item.RelativePath), tocPath, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) return index;
        }
        return saved;
    }

    public static string NormalizeForWebView(string source, string baseHref)
    {
        source ??= string.Empty;
        baseHref ??= "https://pagearc.local/";

        var html = Regex.Replace(source, @"^\s*<\?xml[^>]*\?>\s*", string.Empty, RegexOptions.IgnoreCase);
        html = Regex.Replace(
            html,
            @"<meta\b[^>]*http-equiv\s*=\s*[""']Content-Security-Policy[""'][^>]*>",
            string.Empty,
            RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"\bxlink:href\s*=", "href=", RegexOptions.IgnoreCase);

        var injection = $"<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><base href=\"{WebUtility.HtmlEncode(baseHref)}\">";
        var head = Regex.Match(html, @"<head\b[^>]*>", RegexOptions.IgnoreCase);
        if (head.Success)
            return html.Insert(head.Index + head.Length, injection);

        var root = Regex.Match(html, @"<html\b[^>]*>", RegexOptions.IgnoreCase);
        if (root.Success)
            return html.Insert(root.Index + root.Length, $"<head>{injection}</head>");

        return $"<!doctype html><html><head>{injection}</head><body>{html}</body></html>";
    }

    public static string ExtractReadableText(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return string.Empty;
        var text = Regex.Replace(source, @"<(script|style)\b[^>]*>.*?</\1>", string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<(br|hr)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</(p|div|section|article|h[1-6]|li|blockquote|tr)>\s*", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", string.Empty, RegexOptions.Singleline);
        text = WebUtility.HtmlDecode(text).Replace('\u00a0', ' ');
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = text.Split('\n')
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));
        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private static string BuildBaseHref(string relativePath)
    {
        // Use the original chapter URL as the base. This preserves both normal relative
        // resources (../stylesheet.css, images/a.jpg) and fragment-only links (#note-1).
        var webPath = EpubPath.ToWebPath(relativePath);
        return string.IsNullOrWhiteSpace(webPath)
            ? "https://pagearc.local/"
            : $"https://pagearc.local/{webPath}";
    }

    private static string ResolveSafePath(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, EpubPath.Normalize(relativePath).Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("EPUB chapter path escapes the extracted book cache.");
        return candidate;
    }
}
