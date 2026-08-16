using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using PageArc.Models;

namespace PageArc.Services;

public sealed record EpubRenderChapter(string WebPath, string Html);

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

        var renderDirectory = Path.Combine(document.ExtractionRoot, "__pagearc");
        Directory.CreateDirectory(renderDirectory);
        var renderFileName = $"spine-{spineIndex:D4}.html";
        var renderPath = Path.Combine(renderDirectory, renderFileName);
        await File.WriteAllTextAsync(renderPath, html, new UTF8Encoding(false), cancellationToken);
        return new EpubRenderChapter(EpubPath.ToWebPath($"__pagearc/{renderFileName}"), html);
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

        var injection = $"<meta charset=\"utf-8\"><base href=\"{WebUtility.HtmlEncode(baseHref)}\">";
        var head = Regex.Match(html, @"<head\b[^>]*>", RegexOptions.IgnoreCase);
        if (head.Success)
            return html.Insert(head.Index + head.Length, injection);

        var root = Regex.Match(html, @"<html\b[^>]*>", RegexOptions.IgnoreCase);
        if (root.Success)
            return html.Insert(root.Index + root.Length, $"<head>{injection}</head>");

        return $"<!doctype html><html><head>{injection}</head><body>{html}</body></html>";
    }

    private static string BuildBaseHref(string relativePath)
    {
        var normalized = EpubPath.Normalize(relativePath);
        var slash = normalized.LastIndexOf('/');
        if (slash < 0) return "https://pagearc.local/";
        var directory = normalized[..slash];
        var webDirectory = EpubPath.ToWebPath(directory);
        return string.IsNullOrWhiteSpace(webDirectory)
            ? "https://pagearc.local/"
            : $"https://pagearc.local/{webDirectory}/";
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
