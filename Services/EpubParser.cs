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
            var safeRoot = Path.GetFullPath(extractionRoot) + Path.DirectorySeparatorChar;
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
            .Select(x => new
            {
                Id = (string?)x.Attribute("id"),
                Href = (string?)x.Attribute("href"),
                MediaType = (string?)x.Attribute("media-type"),
                Properties = (string?)x.Attribute("properties")
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Href))
            .ToDictionary(x => x.Id!, x => x, StringComparer.Ordinal);

        var spine = opf.Descendants()
            .Where(x => x.Name.LocalName == "itemref")
            .Select(x => (string?)x.Attribute("idref"))
            .Where(x => !string.IsNullOrWhiteSpace(x) && manifest.ContainsKey(x!))
            .Select(id =>
            {
                var item = manifest[id!];
                return new EpubSpineItem(id!, EpubPath.Combine(packageDirectory, item.Href!), item.MediaType ?? "application/xhtml+xml");
            })
            .ToList();

        if (spine.Count == 0) throw new InvalidDataException("EPUB spine is empty.");

        var toc = new List<EpubTocItem>();
        var navItem = manifest.Values.FirstOrDefault(x =>
            (x.Properties ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains("nav", StringComparer.OrdinalIgnoreCase));

        if (navItem is not null)
        {
            var navPath = EpubPath.Combine(packageDirectory, navItem.Href!);
            var navEntry = GetEntry(archive, navPath);
            await using var navStream = navEntry.Open();
            var nav = await XDocument.LoadAsync(navStream, LoadOptions.None, cancellationToken);
            foreach (var link in nav.Descendants().Where(x => x.Name.LocalName == "a"))
            {
                var href = ((string?)link.Attribute("href"))?.Trim();
                var text = string.Concat(link.DescendantNodes().OfType<XText>()).Trim();
                if (!string.IsNullOrWhiteSpace(href) && !string.IsNullOrWhiteSpace(text))
                    toc.Add(new(text, EpubPath.Combine(Path.GetDirectoryName(navPath)?.Replace('\\', '/') ?? string.Empty, href)));
            }
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

    private static string ReadPackagePath(ZipArchive archive)
    {
        var container = GetEntry(archive, "META-INF/container.xml");
        using var stream = container.Open();
        var document = XDocument.Load(stream);
        var path = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "rootfile")?.Attribute("full-path")?.Value;
        return string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException("EPUB package path is missing.")
            : EpubPath.Normalize(path);
    }

    private static ZipArchiveEntry GetEntry(ZipArchive archive, string path)
    {
        var wanted = EpubPath.Normalize(path);
        return archive.Entries.FirstOrDefault(x =>
            string.Equals(EpubPath.Normalize(x.FullName), wanted, StringComparison.Ordinal))
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
}
