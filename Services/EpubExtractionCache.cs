using System.IO.Compression;

namespace PageArc.Services;

/// <summary>
/// An extraction cache keyed to the source archive's size and last modification time.
/// Incomplete extracts are never promoted to the active book directory.
/// </summary>
public static class EpubExtractionCache
{
    private const string StampName = ".pagearc-extraction.stamp";

    public static async Task EnsureAsync(
        ZipArchive archive,
        string sourcePath,
        string extractionRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var file = new FileInfo(sourcePath);
        var stamp = $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";
        var stampPath = Path.Combine(extractionRoot, StampName);
        if (Directory.Exists(extractionRoot) &&
            File.Exists(Path.Combine(extractionRoot, "META-INF", "container.xml")) &&
            File.Exists(stampPath) &&
            string.Equals(await File.ReadAllTextAsync(stampPath, cancellationToken).ConfigureAwait(false),
                stamp, StringComparison.Ordinal))
            return;

        var stage = extractionRoot + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stage);
        try
        {
            var safeRoot = Path.GetFullPath(stage).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var relativePath = EpubPath.Normalize(entry.FullName);
                if (string.IsNullOrWhiteSpace(relativePath)) continue;
                var target = Path.GetFullPath(Path.Combine(stage,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("EPUB contains an unsafe path.");

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var input = entry.Open();
                await using var output = File.Create(target);
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
            await File.WriteAllTextAsync(Path.Combine(stage, StampName), stamp, cancellationToken)
                .ConfigureAwait(false);
            if (Directory.Exists(extractionRoot)) Directory.Delete(extractionRoot, recursive: true);
            Directory.Move(stage, extractionRoot);
        }
        finally
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true);
        }
    }
}
