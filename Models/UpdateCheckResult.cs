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
    RequestFailed,
    StoreManaged
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version LocalVersion,
    Version? RemoteVersion = null,
    Uri? ReleaseUri = null,
    string? ReleaseName = null,
    string? ReleaseNotes = null,
    Uri? InstallerUri = null,
    string? InstallerName = null,
    long InstallerSize = 0);
