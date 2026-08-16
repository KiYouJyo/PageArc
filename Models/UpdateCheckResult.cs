namespace PageArc.Models;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    NoRelease,
    RateLimited,
    InvalidResponse,
    ConnectionFailed,
    TimedOut,
    RequestFailed
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version LocalVersion,
    Version? RemoteVersion = null,
    Uri? ReleaseUri = null,
    string? ReleaseName = null,
    string? ReleaseNotes = null);
