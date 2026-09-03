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
    long InstallerSize = 0,
    string? ReleaseTag = null,
    Uri? ChecksumUri = null);

public enum UpdateInstallStatus
{
    Completed,
    RestartRequired,
    Canceled,
    Failed
}

public sealed record UpdateInstallResult(UpdateInstallStatus Status, string? ErrorMessage = null);