using System.Globalization;

namespace PageArc.Models;

/// <summary>
/// Remote PageArc backup entry shown in the WebDAV restore/manage dialogs.
/// The display and filename parsing contract intentionally mirrors
/// UrbanPlanToolbox CloudBackupItem @ 249bbf99088e5edc92b9a6f9b7635ca777cf847e.
/// </summary>
public sealed record WebDavArchiveItem(
    string FileName,
    long Size,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? LastModifiedAtUtc,
    string? AppVersion)
{
    public DateTimeOffset SortTimeUtc => CreatedAtUtc ?? LastModifiedAtUtc ?? DateTimeOffset.MinValue;

    public static string CreateFileName(DateTimeOffset createdAtUtc, string appVersion)
    {
        var version = string.IsNullOrWhiteSpace(appVersion) ? "unknown" : appVersion.Trim().TrimStart('v');
        return $"PageArc-{createdAtUtc.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}-v{version}.pagearcbackup";
    }

    public static bool TryParseFileName(string fileName, out DateTimeOffset createdAtUtc, out string version)
    {
        const string prefix = "PageArc-";
        const string suffix = ".pagearcbackup";
        createdAtUtc = default;
        version = string.Empty;

        if (!fileName.StartsWith(prefix, StringComparison.Ordinal)
            || !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var core = fileName[prefix.Length..^suffix.Length];
        var marker = core.LastIndexOf("-v", StringComparison.Ordinal);
        if (marker <= 0 || marker >= core.Length - 2) return false;

        var timestamp = core[..marker];
        version = core[(marker + 2)..];
        return DateTimeOffset.TryParseExact(
            timestamp,
            "yyyyMMdd'T'HHmmss'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out createdAtUtc);
    }

    public static bool IsPageArcBackupFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && !fileName.Contains('/')
        && !fileName.Contains('\\')
        && fileName.EndsWith(".pagearcbackup", StringComparison.OrdinalIgnoreCase);
}

public sealed record WebDavArchiveListResult(
    IReadOnlyList<WebDavArchiveItem> Items,
    bool Succeeded,
    string? ErrorCode = null);

public readonly record struct WebDavTransferProgress(long BytesTransferred, long? TotalBytes)
{
    public double Fraction => TotalBytes is > 0
        ? Math.Clamp((double)BytesTransferred / TotalBytes.Value, 0d, 1d)
        : 0d;
}
