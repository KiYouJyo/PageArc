namespace PageArc.Services;

public static class CoverCacheService
{
    private const long MaxCoverBytes = 32L * 1024L * 1024L;

    public static async Task<string?> SaveDataUrlAsync(
        string bookId,
        string? dataUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bookId) || string.IsNullOrWhiteSpace(dataUrl)) return null;
        if (!dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) return null;

        var comma = dataUrl.IndexOf(',');
        if (comma <= 5 || comma >= dataUrl.Length - 1) return null;
        var metadata = dataUrl[5..comma];
        var payload = dataUrl[(comma + 1)..];
        if (!metadata.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;

        var mediaType = metadata[..^7].Trim().ToLowerInvariant();
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return null;
        }
        if (bytes.LongLength == 0 || bytes.LongLength > MaxCoverBytes) return null;

        var extension = mediaType switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            _ => null
        };
        if (extension is null) return null;

        AppPaths.Ensure();
        foreach (var existing in Directory.EnumerateFiles(AppPaths.CoversRoot, bookId + ".*"))
        {
            try { File.Delete(existing); } catch { }
        }

        var destination = Path.Combine(AppPaths.CoversRoot, bookId + extension);
        await File.WriteAllBytesAsync(destination, bytes, cancellationToken);
        return destination;
    }
}
