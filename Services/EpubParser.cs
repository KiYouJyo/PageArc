using System.IO.Compression;
using System.Xml.Linq;
using PageArc.Models;

namespace PageArc.Services;

public static class EpubParser
{
    private static readonly HashSet<string> SupportedFontObfuscationAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "http://www.idpf.org/2008/embedding",
        "http://ns.adobe.com/pdf/enc#RC"
    };

    public sealed record Metadata(string Title, string Author);

    public static async Task<Metadata> ReadMetadataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using var archive = ZipFile.OpenRead(filePath);
        var packagePath = ReadPackagePath(archive);
        var packageEntry = GetEntry(archive, packagePath);
        await using var stream = packageEntry.Open();
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var title = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "title")?.Value?.Trim();
        var author = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "creator")?.Value?.Trim();
        return new(title ?? Path.GetFileNameWithoutExtension(filePath), author ?? string.Empty);
    }

    public static async Task<EpubDocument> OpenAsync(BookEntry book, CancellationToken cancellationToken = default)
    {
        if (!book.Format.Equals("EPUB", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("PageArc v0.1.0 reader core currently opens EPUB files.");

        var extractionRoot = Path.Combine(AppPaths.BooksCacheRoot, book.Id);
        if (Directory.Exists(extractionRoot)) Directory.Delete(extractionRoot, true);
        Directory.CreateDirectory(extractionRoot);

        using var archive = ZipFile.OpenRead(book.FilePath);
        if (HasUnsupportedEncryption(archive))
            throw new InvalidDataException("This EPUB contains encrypted/DRM content that PageArc cannot open.");

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name)) continue;

            var logicalPath = EpubPath.Normalize(entry.FullName);
            if (string.IsNullOrWhiteSpace(logicalPath)) continue;
            var target = Path.GetFullPath(Path.Combine(extractionRoot, logicalPath.Replace('/', Path.DirectorySeparatorChar)));
            var safeRoot = Path.GetFullPath(extractionRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("EPUB contains an unsafe path.");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var source = entry.Open();
            await using var destination = File.Create(target);
            await source.CopyToAsync(destination, cancellationToken);
        }

        var packagePath = ReadPackagePath(archive);
        var packageEntry = GetEntry(archive, packagePath);
        await using var packageStream = packageEntry.Open();
        var opf = await XDocument.LoadAsync(packageStream, LoadOptions.None, cancellationToken);
        var packageDirectory = Path.GetDirectoryName(packagePath)?.Replace('\\', '/') ?? string.Empty;

        var metadata = opf.Descendants().FirstOrDefault(x => x.Name.LocalName == "metadata");
        var title = metadata?.Descendants().FirstOrDefault(x => x.Name.LocalName == "title")?.Value?.Trim() ?? book.Title;
        var author = metadata?.Descendants().FirstOrDefault(x => x.Name.LocalName == "creator")?.Value?.Trim() ?? book.Author;

        var manifest = opf.Descendants()
            .Where(x => x.Name.LocalName == "item")
            .Select(x => new ManifestItem(
                (string?)x.Attribute("id") ?? string.Empty,
                (string?)x.Attribute("href") ?? string.Empty,
                (string?)x.Attribute("media-type") ?? string.Empty,
                (string?)x.Attribute("properties") ?? string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Href))
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);

        var spineElement = opf.Descendants().FirstOrDefault(x => x.Name.LocalName == "spine");
        var spine = opf.Descendants()
            .Where(x => x.Name.LocalName == "itemref")
            .Select(x => (string?)x.Attribute("idref"))
            .Where(x => !string.IsNullOrWhiteSpace(x) && manifest.ContainsKey(x!))
            .Select(id =>
            {
                var item = manifest[id!];
                return new EpubSpineItem(id!, EpubPath.Combine(packageDirectory, item.Href), string.IsNullOrWhiteSpace(item.MediaType) ? "application/xhtml+xml" : item.MediaType);
            })
            .ToList();

        if (spine.Count == 0) throw new InvalidDataException("EPUB spine is empty.");

        var toc = new List<EpubTocItem>();
        var navItem = manifest.Values.FirstOrDefault(x =>
            x.Properties.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains("nav", StringComparer.OrdinalIgnoreCase));
        if (navItem is not null)
            await TryReadNavigationAsync(archive, packageDirectory, navItem, toc, cancellationToken);

        if (toc.Count == 0)
        {
            var ncxId = (string?)spineElement?.Attribute("toc");
            ManifestItem? ncxItem = null;
            if (!string.IsNullOrWhiteSpace(ncxId)) manifest.TryGetValue(ncxId, out ncxItem);
            ncxItem ??= manifest.Values.FirstOrDefault(x => string.Equals(x.MediaType, "application/x-dtbncx+xml", StringComparison.OrdinalIgnoreCase));
            if (ncxItem is not null)
                await TryReadNcxAsync(archive, packageDirectory, ncxItem, toc, cancellationToken);
        }

        return new EpubDocument
        {
            Title = title,
            Author = author,
            ExtractionRoot = extractionRoot,
            PackagePath = packagePath,
            Spine = spine,
            Toc = toc
        };
    }

    private static async Task TryReadNavigationAsync(
        ZipArchive archive,
        string packageDirectory,
        ManifestItem navItem,
        List<EpubTocItem> toc,
        CancellationToken cancellationToken)
    {
        try
        {
            var navPath = EpubPath.Combine(packageDirectory, navItem.Href);
            var navEntry = GetEntry(archive, navPath);
            await using var navStream = navEntry.Open();
            var nav = await XDocument.LoadAsync(navStream, LoadOptions.None, cancellationToken);
            var navDirectory = Path.GetDirectoryName(navPath)?.Replace('\\', '/') ?? string.Empty;
            foreach (var link in nav.Descendants().Where(x => x.Name.LocalName == "a"))
            {
                var href = ((string?)link.Attribute("href"))?.Trim();
                var text = string.Concat(link.DescendantNodes().OfType<XText>()).Trim();
                if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(text)) continue;
                toc.Add(new EpubTocItem(text, CombinePreservingFragment(navDirectory, href)));
            }
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("EPUB navigation document could not be parsed; continuing without EPUB3 nav", ex);
        }
    }

    private static async Task TryReadNcxAsync(
        ZipArchive archive,
        string packageDirectory,
        ManifestItem ncxItem,
        List<EpubTocItem> toc,
        CancellationToken cancellationToken)
    {
        try
        {
            var ncxPath = EpubPath.Combine(packageDirectory, ncxItem.Href);
            var ncxEntry = GetEntry(archive, ncxPath);
            await using var stream = ncxEntry.Open();
            var ncx = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
            var directory = Path.GetDirectoryName(ncxPath)?.Replace('\\', '/') ?? string.Empty;
            foreach (var navPoint in ncx.Descendants().Where(x => x.Name.LocalName == "navPoint"))
            {
                var text = navPoint.Descendants().FirstOrDefault(x => x.Name.LocalName == "text")?.Value?.Trim();
                var src = navPoint.Descendants().FirstOrDefault(x => x.Name.LocalName == "content")?.Attribute("src")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(src))
                    toc.Add(new EpubTocItem(text, CombinePreservingFragment(directory, src)));
            }
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("EPUB NCX document could not be parsed; continuing without EPUB2 TOC", ex);
        }
    }

    private static string CombinePreservingFragment(string directory, string href)
    {
        var hash = href.IndexOf('#');
        var path = hash >= 0 ? href[..hash] : href;
        var fragment = hash >= 0 ? href[hash..] : string.Empty;
        return EpubPath.Combine(directory, path) + fragment;
    }

    private static string ReadPackagePath(ZipArchive archive)
    {
        var container = GetEntry(archive, "META-INF/container.xml");
        using var stream = container.Open();
        var document = XDocument.Load(stream);
        var rootFiles = document.Descendants().Where(x => x.Name.LocalName == "rootfile").ToList();
        var preferred = rootFiles.FirstOrDefault(x =>
            string.Equals((string?)x.Attribute("media-type"), "application/oebps-package+xml", StringComparison.OrdinalIgnoreCase))
            ?? rootFiles.FirstOrDefault();
        var path = preferred?.Attribute("full-path")?.Value;
        return string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException("EPUB package path is missing.")
            : EpubPath.Normalize(path);
    }

    private static ZipArchiveEntry GetEntry(ZipArchive archive, string path)
    {
        var wanted = EpubPath.Normalize(path);
        return archive.Entries.FirstOrDefault(x =>
            string.Equals(EpubPath.Normalize(x.FullName), wanted, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"EPUB entry not found: {path}");
    }

    private static bool HasUnsupportedEncryption(ZipArchive archive)
    {
        var encryptionEntry = archive.Entries.FirstOrDefault(x =>
            string.Equals(EpubPath.Normalize(x.FullName), "META-INF/encryption.xml", StringComparison.OrdinalIgnoreCase));
        if (encryptionEntry is null) return false;

        try
        {
            using var stream = encryptionEntry.Open();
            var document = XDocument.Load(stream);
            var encryptedData = document.Descendants().Where(x => x.Name.LocalName == "EncryptedData").ToList();
            if (encryptedData.Count == 0) return false;

            foreach (var data in encryptedData)
            {
                var algorithm = data.Descendants()
                    .FirstOrDefault(x => x.Name.LocalName == "EncryptionMethod")?
                    .Attribute("Algorithm")?.Value;
                if (string.IsNullOrWhiteSpace(algorithm) || !SupportedFontObfuscationAlgorithms.Contains(algorithm))
                    return true;
            }
            return false;
        }
        catch
        {
            return true;
        }
    }

    private sealed record ManifestItem(string Id, string Href, string MediaType, string Properties);
}
