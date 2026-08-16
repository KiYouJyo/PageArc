using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PageArc.Models;

namespace PageArc.Services;

public static class BookMetadataService
{
    private const long MaxCoverBytes = 32L * 1024L * 1024L;

    public static Task<BookMetadataSnapshot> ReadAsync(BookEntry book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        return BookFormatRegistry.Normalize(book.Format) switch
        {
            "EPUB" => ReadEpubAsync(book, cancellationToken),
            "FB2" => ReadFb2Async(book, cancellationToken),
            _ => Task.FromResult(BookMetadataSnapshot.Empty(book.Title))
        };
    }

    private static async Task<BookMetadataSnapshot> ReadEpubAsync(BookEntry book, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(book.FilePath);
        var packagePath = ReadEpubPackagePath(archive);
        var packageEntry = GetArchiveEntry(archive, packagePath);
        await using var packageStream = packageEntry.Open();
        var opf = await XDocument.LoadAsync(packageStream, LoadOptions.None, cancellationToken);
        var metadata = opf.Descendants().FirstOrDefault(x => x.Name.LocalName == "metadata");
        var title = Value(metadata, "title") ?? book.Title;
        var author = Value(metadata, "creator") ?? book.Author;
        var language = Value(metadata, "language") ?? string.Empty;
        var publisher = Value(metadata, "publisher") ?? string.Empty;
        var description = NormalizeText(Value(metadata, "description"));

        var manifest = opf.Descendants()
            .Where(x => x.Name.LocalName == "item")
            .Select(x => new EpubManifestItem(
                (string?)x.Attribute("id") ?? string.Empty,
                (string?)x.Attribute("href") ?? string.Empty,
                (string?)x.Attribute("media-type") ?? string.Empty,
                (string?)x.Attribute("properties") ?? string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Href))
            .ToArray();

        EpubManifestItem? cover = manifest.FirstOrDefault(x =>
            x.Properties.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("cover-image", StringComparer.OrdinalIgnoreCase));
        if (cover is null)
        {
            var coverId = metadata?.Descendants()
                .FirstOrDefault(x => x.Name.LocalName == "meta"
                    && string.Equals((string?)x.Attribute("name"), "cover", StringComparison.OrdinalIgnoreCase))?
                .Attribute("content")?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(coverId))
                cover = manifest.FirstOrDefault(x => string.Equals(x.Id, coverId, StringComparison.Ordinal));
        }
        cover ??= manifest.FirstOrDefault(x =>
            x.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            && x.Id.Contains("cover", StringComparison.OrdinalIgnoreCase));

        string? coverPath = null;
        if (cover is not null)
        {
            var packageDirectory = Path.GetDirectoryName(packagePath)?.Replace('\\', '/') ?? string.Empty;
            var logicalPath = EpubPath.Combine(packageDirectory, cover.Href);
            var entry = TryGetArchiveEntry(archive, logicalPath);
            if (entry is not null && entry.Length <= MaxCoverBytes)
                coverPath = await WriteCoverAsync(book.Id, entry.Open(), cover.MediaType, Path.GetExtension(cover.Href), cancellationToken);
        }

        return new BookMetadataSnapshot(title, author, language, publisher, description, coverPath);
    }

    private static async Task<BookMetadataSnapshot> ReadFb2Async(BookEntry book, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(book.FilePath);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var description = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "description");
        var titleInfo = description?.Descendants().FirstOrDefault(x => x.Name.LocalName == "title-info")
            ?? document.Descendants().FirstOrDefault(x => x.Name.LocalName == "title-info");
        var publishInfo = description?.Descendants().FirstOrDefault(x => x.Name.LocalName == "publish-info")
            ?? document.Descendants().FirstOrDefault(x => x.Name.LocalName == "publish-info");

        var title = titleInfo?.Elements().FirstOrDefault(x => x.Name.LocalName == "book-title")?.Value?.Trim();
        var authorElement = titleInfo?.Elements().FirstOrDefault(x => x.Name.LocalName == "author");
        var author = FormatFb2Author(authorElement);
        var language = titleInfo?.Elements().FirstOrDefault(x => x.Name.LocalName == "lang")?.Value?.Trim() ?? string.Empty;
        var publisher = publishInfo?.Elements().FirstOrDefault(x => x.Name.LocalName == "publisher")?.Value?.Trim() ?? string.Empty;
        var annotation = titleInfo?.Elements().FirstOrDefault(x => x.Name.LocalName == "annotation");
        var descriptionText = NormalizeText(annotation?.Value);

        string? coverPath = null;
        var coverPage = titleInfo?.Elements().FirstOrDefault(x => x.Name.LocalName == "coverpage");
        var image = coverPage?.Descendants().FirstOrDefault(x => x.Name.LocalName == "image");
        var href = image?.Attributes().FirstOrDefault(x => x.Name.LocalName == "href")?.Value?.Trim();
        var coverId = href?.TrimStart('#');
        if (!string.IsNullOrWhiteSpace(coverId))
        {
            var binary = document.Descendants().FirstOrDefault(x =>
                x.Name.LocalName == "binary"
                && string.Equals((string?)x.Attribute("id"), coverId, StringComparison.Ordinal));
            if (binary is not null)
            {
                var mediaType = (string?)binary.Attribute("content-type") ?? "application/octet-stream";
                var encoded = string.Concat(binary.Nodes().OfType<XText>().Select(x => x.Value)).Trim();
                if (!string.IsNullOrWhiteSpace(encoded))
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(encoded);
                        if (bytes.LongLength <= MaxCoverBytes)
                        {
                            await using var coverStream = new MemoryStream(bytes, writable: false);
                            coverPath = await WriteCoverAsync(book.Id, coverStream, mediaType, string.Empty, cancellationToken);
                        }
                    }
                    catch (FormatException ex)
                    {
                        StartupDiagnostics.Log($"FB2 cover image for '{book.FilePath}' is not valid base64.", ex);
                    }
                }
            }
        }

        return new BookMetadataSnapshot(
            string.IsNullOrWhiteSpace(title) ? book.Title : title,
            string.IsNullOrWhiteSpace(author) ? book.Author : author,
            language,
            publisher,
            descriptionText,
            coverPath);
    }

    private static async Task<string> WriteCoverAsync(
        string bookId,
        Stream source,
        string mediaType,
        string suggestedExtension,
        CancellationToken cancellationToken)
    {
        AppPaths.Ensure();
        var extension = ResolveCoverExtension(mediaType, suggestedExtension);
        foreach (var existing in Directory.EnumerateFiles(AppPaths.CoversRoot, bookId + ".*"))
        {
            try { File.Delete(existing); } catch { }
        }
        var destination = Path.Combine(AppPaths.CoversRoot, bookId + extension);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
        await source.CopyToAsync(output, cancellationToken);
        return destination;
    }

    private static string ResolveCoverExtension(string mediaType, string suggestedExtension)
    {
        var normalizedType = mediaType.Trim().ToLowerInvariant();
        return normalizedType switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            _ when suggestedExtension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) => ".jpg",
            _ when new[] { ".jpg", ".png", ".gif", ".webp", ".bmp" }.Contains(suggestedExtension, StringComparer.OrdinalIgnoreCase)
                => suggestedExtension.ToLowerInvariant(),
            _ => ".img"
        };
    }

    private static string ReadEpubPackagePath(ZipArchive archive)
    {
        var container = GetArchiveEntry(archive, "META-INF/container.xml");
        using var stream = container.Open();
        var document = XDocument.Load(stream);
        var rootFiles = document.Descendants().Where(x => x.Name.LocalName == "rootfile").ToArray();
        var preferred = rootFiles.FirstOrDefault(x =>
            string.Equals((string?)x.Attribute("media-type"), "application/oebps-package+xml", StringComparison.OrdinalIgnoreCase))
            ?? rootFiles.FirstOrDefault();
        var path = preferred?.Attribute("full-path")?.Value;
        return string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException("EPUB package path is missing.")
            : EpubPath.Normalize(path);
    }

    private static ZipArchiveEntry GetArchiveEntry(ZipArchive archive, string path) =>
        TryGetArchiveEntry(archive, path) ?? throw new InvalidDataException($"EPUB entry not found: {path}");

    private static ZipArchiveEntry? TryGetArchiveEntry(ZipArchive archive, string path)
    {
        var normalized = EpubPath.Normalize(path);
        return archive.Entries.FirstOrDefault(x =>
            string.Equals(EpubPath.Normalize(x.FullName), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Value(XElement? parent, string localName) =>
        parent?.Descendants().FirstOrDefault(x => x.Name.LocalName == localName)?.Value?.Trim();

    private static string FormatFb2Author(XElement? author)
    {
        if (author is null) return string.Empty;
        var parts = new[]
        {
            author.Elements().FirstOrDefault(x => x.Name.LocalName == "first-name")?.Value?.Trim(),
            author.Elements().FirstOrDefault(x => x.Name.LocalName == "middle-name")?.Value?.Trim(),
            author.Elements().FirstOrDefault(x => x.Name.LocalName == "last-name")?.Value?.Trim()
        }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (parts.Length > 0) return string.Join(" ", parts!);
        return author.Elements().FirstOrDefault(x => x.Name.LocalName == "nickname")?.Value?.Trim() ?? string.Empty;
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return Regex.Replace(value, "\\s+", " ").Trim();
    }

    private sealed record EpubManifestItem(string Id, string Href, string MediaType, string Properties);
}
