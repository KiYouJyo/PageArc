using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PageArc.Services.Conversion;

public sealed record ConversionRuntimeManifest(
    int SchemaVersion,
    string RuntimeId,
    string PackageVersion,
    string CalibreVersion,
    string MinimumPageArcVersion,
    string Platform,
    string Architecture,
    string ArchiveFileName,
    long ArchiveSize,
    string Sha256,
    string ExecutableRelativePath,
    string SourceFileName);

public readonly record struct ConversionRuntimeProgress(
    string Stage,
    long BytesTransferred,
    long? TotalBytes)
{
    public double Fraction => TotalBytes is > 0
        ? Math.Clamp((double)BytesTransferred / TotalBytes.Value, 0d, 1d)
        : 0d;
}

public sealed record ConversionRuntimeStatus(
    bool IsSupported,
    bool IsInstalled,
    string PackageVersion,
    string CalibreVersion,
    string? ExecutablePath,
    long InstalledBytes);

public sealed record ConversionRuntimeRelease(
    string TagName,
    ConversionRuntimeManifest Manifest,
    Uri ManifestUri,
    Uri ArchiveUri,
    DateTimeOffset? PublishedAt);

public sealed record ConversionRuntimeUpdateCheck(
    bool Succeeded,
    ConversionRuntimeStatus LocalStatus,
    ConversionRuntimeRelease? LatestCompatibleRelease,
    bool UpdateAvailable,
    string? ErrorCode = null);

public sealed record ConversionRuntimeOperationState(
    bool IsBusy,
    string Stage,
    long BytesTransferred,
    long? TotalBytes,
    string? PackageVersion,
    string? ErrorCode = null)
{
    public double Fraction => TotalBytes is > 0
        ? Math.Clamp((double)BytesTransferred / TotalBytes.Value, 0d, 1d)
        : 0d;

    public static ConversionRuntimeOperationState Idle { get; } =
        new(false, "idle", 0, null, null);
}

/// <summary>
/// Owns PageArc's optional conversion-runtime lifecycle.
/// Runtime binaries live in KiYouJyo/PageArc.ConversionRuntime releases and are never embedded in the PageArc MSIX.
/// </summary>
public sealed class ConversionRuntimeManager
{
    public const string RuntimeId = "pagearc-calibre";
    public const string CurrentPageArcCompatibilityVersion = "1.4.0";
    public const string PackageVersion = "9.13.0-pagearc.1";
    public const string CalibreVersion = "9.13.0";
    public const string ReleaseTag = "v9.13.0-pagearc.1";
    public const string ArchiveFileName = "PageArc.ConversionRuntime-win-x64.zip";
    public const string ExecutableRelativePath = "runtime/ebook-convert.exe";
    public const long ExpectedArchiveSize = 282915121;
    public const string ExpectedArchiveSha256 = "1d223227254d6dfacc8f5645caf3cba26434e129cf5bb65decb0a121a61b5322";

    public static readonly Uri RepositoryUri = new("https://github.com/KiYouJyo/PageArc.ConversionRuntime");
    public static readonly Uri LatestReleaseApiUri = new("https://api.github.com/repos/KiYouJyo/PageArc.ConversionRuntime/releases/latest");
    public static readonly Uri ManifestUri =
        new($"https://github.com/KiYouJyo/PageArc.ConversionRuntime/releases/download/{ReleaseTag}/runtime-manifest.json");
    public static readonly Uri ArchiveUri =
        new($"https://github.com/KiYouJyo/PageArc.ConversionRuntime/releases/download/{ReleaseTag}/{ArchiveFileName}");

    public static ConversionRuntimeManager Shared { get; } = new();

    private static readonly SemaphoreSlim InstallGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _client;
    private readonly object _operationStateGate = new();
    private ConversionRuntimeOperationState _operationState = ConversionRuntimeOperationState.Idle;

    public event EventHandler<ConversionRuntimeOperationState>? OperationStateChanged;

    public ConversionRuntimeOperationState OperationState
    {
        get
        {
            lock (_operationStateGate)
                return _operationState;
        }
    }

    public ConversionRuntimeUpdateCheck? LastUpdateCheck { get; private set; }

    public ConversionRuntimeManager(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
    }

    public bool IsSupported =>
        OperatingSystem.IsWindows()
        && RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.Arm64;

    // Kept for compatibility with the original v1.4 pinned contract.
    public string InstallRoot => GetInstallRoot(PackageVersion);
    public string ExecutablePath => GetStatus().ExecutablePath ?? Path.Combine(
        InstallRoot,
        ExecutableRelativePath.Replace('/', Path.DirectorySeparatorChar));
    public bool IsInstalled => GetStatus().IsInstalled;

    public ConversionRuntimeStatus GetStatus()
    {
        var installed = FindInstalledRuntimes()
            .OrderByDescending(item => item.Manifest.PackageVersion, RuntimePackageVersionComparer.Instance)
            .FirstOrDefault();

        if (installed is null)
        {
            return new ConversionRuntimeStatus(
                IsSupported,
                false,
                PackageVersion,
                CalibreVersion,
                null,
                0);
        }

        return new ConversionRuntimeStatus(
            IsSupported,
            true,
            installed.Manifest.PackageVersion,
            installed.Manifest.CalibreVersion,
            installed.ExecutablePath,
            installed.Bytes);
    }

    public async Task<ConversionRuntimeUpdateCheck> CheckForUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        var local = GetStatus();
        ConversionRuntimeUpdateCheck result;
        if (!IsSupported)
        {
            result = new(false, local, null, false, "UnsupportedPlatform");
            LastUpdateCheck = result;
            return result;
        }

        try
        {
            var release = await GetLatestCompatibleReleaseAsync(cancellationToken);
            var updateAvailable = release is not null
                && (!local.IsInstalled
                    || RuntimePackageVersionComparer.Instance.Compare(
                        release.Manifest.PackageVersion,
                        local.PackageVersion) > 0);

            result = new(true, local, release, updateAvailable);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = new(false, local, null, false, "Timeout");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or FormatException)
        {
            StartupDiagnostics.Log("Conversion runtime update check failed", ex);
            result = new(false, local, null, false, ex.GetType().Name);
        }

        LastUpdateCheck = result;
        return result;
    }

    public async Task<string> EnsureInstalledAsync(
        IProgress<ConversionRuntimeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var status = GetStatus();
        if (!IsSupported)
            throw new PlatformNotSupportedException("The PageArc conversion runtime is currently distributed for Windows x64-compatible systems only.");
        if (status.IsInstalled && !string.IsNullOrWhiteSpace(status.ExecutablePath))
            return status.ExecutablePath;

        var pinned = new ConversionRuntimeRelease(
            ReleaseTag,
            new ConversionRuntimeManifest(
                1,
                RuntimeId,
                PackageVersion,
                CalibreVersion,
                "1.4.0",
                "windows",
                "x64",
                ArchiveFileName,
                ExpectedArchiveSize,
                ExpectedArchiveSha256,
                ExecutableRelativePath,
                $"calibre-{CalibreVersion}.tar.xz"),
            ManifestUri,
            ArchiveUri,
            null);

        return await InstallReleaseAsync(pinned, progress, cancellationToken);
    }

    public async Task<string> InstallLatestCompatibleAsync(
        IProgress<ConversionRuntimeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException("The PageArc conversion runtime is currently distributed for Windows x64-compatible systems only.");

        PublishOperationState(new(true, "manifest", 0, null, null), progress);
        try
        {
            var release = await GetLatestCompatibleReleaseAsync(cancellationToken)
                ?? throw new InvalidDataException("No compatible PageArc conversion runtime release is available.");
            return await InstallReleaseAsync(release, progress, cancellationToken);
        }
        catch (Exception ex)
        {
            PublishOperationState(new(false, "failed", 0, null, null, ex.GetType().Name), progress);
            throw;
        }
    }

    public async Task<string> InstallReleaseAsync(
        ConversionRuntimeRelease release,
        IProgress<ConversionRuntimeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        ValidateManifest(release.Manifest, requirePinnedIdentity: false);
        if (!string.Equals(release.TagName, $"v{release.Manifest.PackageVersion}", StringComparison.Ordinal))
            throw new InvalidDataException("Runtime release tag does not match the manifest package version.");

        await InstallGate.WaitAsync(cancellationToken);
        try
        {
            var manifest = release.Manifest;
            PublishOperationState(new(true, "manifest", 0, manifest.ArchiveSize, manifest.PackageVersion), progress);
            AppPaths.Ensure();
            var targetRoot = GetInstallRoot(manifest.PackageVersion);
            var targetExecutable = Path.Combine(
                targetRoot,
                manifest.ExecutableRelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(targetExecutable) && new FileInfo(targetExecutable).Length > 0)
            {
                PublishOperationState(new(false, "complete", manifest.ArchiveSize, manifest.ArchiveSize, manifest.PackageVersion), progress);
                return targetExecutable;
            }

            var downloadPath = Path.Combine(
                AppPaths.RuntimeDownloadsRoot,
                $"{manifest.PackageVersion}-{Guid.NewGuid():N}.zip.partial");
            var stagingRoot = Path.Combine(
                AppPaths.ConversionRuntimesRoot,
                $".staging-{manifest.PackageVersion}-{Guid.NewGuid():N}");

            try
            {
                await DownloadArchiveAsync(release.ArchiveUri, manifest, downloadPath, progress, cancellationToken);
                await VerifyArchiveAsync(downloadPath, manifest, cancellationToken);

                PublishOperationState(new(true, "extract", 0, manifest.ArchiveSize, manifest.PackageVersion), progress);
                ExtractArchiveSafely(downloadPath, stagingRoot, cancellationToken);

                var stagedExecutable = Path.Combine(
                    stagingRoot,
                    manifest.ExecutableRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(stagedExecutable) || new FileInfo(stagedExecutable).Length == 0)
                    throw new InvalidDataException("The downloaded conversion runtime does not contain a valid ebook-convert executable.");

                await ValidateExecutableAsync(stagedExecutable, manifest.CalibreVersion, cancellationToken);

                Directory.CreateDirectory(Path.GetDirectoryName(targetRoot)!);
                if (Directory.Exists(targetRoot))
                    Directory.Delete(targetRoot, recursive: true);
                Directory.Move(stagingRoot, targetRoot);

                var installedManifest = Path.Combine(targetRoot, "pagearc-runtime-manifest.json");
                await File.WriteAllTextAsync(
                    installedManifest,
                    JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
                    cancellationToken);

                // Keep only the active compatible version after a successful update.
                RemoveOtherRuntimeVersions(manifest.PackageVersion);

                PublishOperationState(new(false, "complete", manifest.ArchiveSize, manifest.ArchiveSize, manifest.PackageVersion), progress);
                return targetExecutable;
            }
            catch (Exception ex)
            {
                PublishOperationState(new(false, "failed", 0, manifest.ArchiveSize, manifest.PackageVersion, ex.GetType().Name), progress);
                throw;
            }
            finally
            {
                TryDeleteFile(downloadPath);
                TryDeleteDirectory(stagingRoot);
            }
        }
        finally
        {
            InstallGate.Release();
        }
    }

    public void RemoveInstalledRuntime()
    {
        try
        {
            if (!Directory.Exists(AppPaths.ConversionRuntimesRoot)) return;
            foreach (var directory in Directory.EnumerateDirectories(AppPaths.ConversionRuntimesRoot))
            {
                if (Path.GetFileName(directory).StartsWith(".staging-", StringComparison.OrdinalIgnoreCase))
                    continue;
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Conversion runtime removal failed", ex);
            throw;
        }
    }

    private async Task<ConversionRuntimeRelease?> GetLatestCompatibleReleaseAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, LatestReleaseApiUri);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var release = JsonSerializer.Deserialize<GitHubReleaseEnvelope>(json, JsonOptions)
            ?? throw new InvalidDataException("The conversion runtime release response is empty.");
        if (release.Draft || release.Prerelease || string.IsNullOrWhiteSpace(release.TagName))
            return null;

        var manifestAsset = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, "runtime-manifest.json", StringComparison.OrdinalIgnoreCase));
        if (manifestAsset is null || !Uri.TryCreate(manifestAsset.BrowserDownloadUrl, UriKind.Absolute, out var manifestUri))
            throw new InvalidDataException("The latest conversion runtime release does not contain runtime-manifest.json.");

        using var manifestRequest = CreateRequest(HttpMethod.Get, manifestUri);
        using var manifestResponse = await _client.SendAsync(manifestRequest, HttpCompletionOption.ResponseContentRead, cancellationToken);
        manifestResponse.EnsureSuccessStatusCode();
        var manifestJson = await manifestResponse.Content.ReadAsStringAsync(cancellationToken);
        var manifest = JsonSerializer.Deserialize<ConversionRuntimeManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidDataException("The latest conversion runtime manifest is empty.");

        ValidateManifest(manifest, requirePinnedIdentity: false);
        if (!string.Equals(release.TagName, $"v{manifest.PackageVersion}", StringComparison.Ordinal))
            throw new InvalidDataException("Latest runtime release tag and manifest package version do not match.");
        if (!IsCompatibleWithCurrentPageArc(manifest.MinimumPageArcVersion))
            return null;

        var archiveAsset = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, manifest.ArchiveFileName, StringComparison.OrdinalIgnoreCase));
        if (archiveAsset is null || !Uri.TryCreate(archiveAsset.BrowserDownloadUrl, UriKind.Absolute, out var archiveUri))
            throw new InvalidDataException("The latest conversion runtime release does not contain its declared archive.");
        if (archiveAsset.Size > 0 && archiveAsset.Size != manifest.ArchiveSize)
            throw new InvalidDataException("The latest conversion runtime asset size does not match its manifest.");

        return new ConversionRuntimeRelease(
            release.TagName,
            manifest,
            manifestUri,
            archiveUri,
            release.PublishedAt);
    }

    private static bool IsCompatibleWithCurrentPageArc(string minimumVersion)
    {
        if (!Version.TryParse(minimumVersion, out var minimum)) return false;
        var current = Version.Parse(CurrentPageArcCompatibilityVersion);
        return current >= minimum;
    }

    private static void ValidateManifest(ConversionRuntimeManifest manifest, bool requirePinnedIdentity)
    {
        var shaValid = manifest.Sha256.Length == 64 && manifest.Sha256.All(Uri.IsHexDigit);
        var archiveNameValid = string.Equals(Path.GetFileName(manifest.ArchiveFileName), manifest.ArchiveFileName, StringComparison.Ordinal)
            && manifest.ArchiveFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        var packageVersionValid = RuntimePackageVersionComparer.TryParse(manifest.PackageVersion, out _, out _);

        if (manifest.SchemaVersion != 1
            || !string.Equals(manifest.RuntimeId, RuntimeId, StringComparison.Ordinal)
            || !string.Equals(manifest.Platform, "windows", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(manifest.Architecture, "x64", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(manifest.ExecutableRelativePath, ExecutableRelativePath, StringComparison.Ordinal)
            || !archiveNameValid
            || !packageVersionValid
            || manifest.ArchiveSize < 1024 * 1024
            || manifest.ArchiveSize > 2L * 1024 * 1024 * 1024
            || !shaValid
            || string.IsNullOrWhiteSpace(manifest.CalibreVersion)
            || !Version.TryParse(manifest.MinimumPageArcVersion, out _))
        {
            throw new InvalidDataException("The conversion runtime manifest is invalid or incompatible.");
        }

        if (requirePinnedIdentity
            && (!string.Equals(manifest.PackageVersion, PackageVersion, StringComparison.Ordinal)
                || !string.Equals(manifest.CalibreVersion, CalibreVersion, StringComparison.Ordinal)
                || !string.Equals(manifest.ArchiveFileName, ArchiveFileName, StringComparison.Ordinal)
                || manifest.ArchiveSize != ExpectedArchiveSize
                || !string.Equals(manifest.Sha256, ExpectedArchiveSha256, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("The conversion runtime manifest does not match PageArc's pinned fallback contract.");
        }
    }

    private async Task DownloadArchiveAsync(
        Uri archiveUri,
        ConversionRuntimeManifest manifest,
        string destinationPath,
        IProgress<ConversionRuntimeProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, archiveUri);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? manifest.ArchiveSize;
        if (total > 0 && total != manifest.ArchiveSize)
            throw new InvalidDataException($"Conversion runtime size mismatch before download: expected {manifest.ArchiveSize}, remote reports {total}.");

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 256, useAsync: true);
        var buffer = new byte[1024 * 256];
        long copied = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
            PublishOperationState(new(true, "download", copied, manifest.ArchiveSize, manifest.PackageVersion), progress);
        }

        await target.FlushAsync(cancellationToken);
        if (copied != manifest.ArchiveSize)
            throw new InvalidDataException($"Conversion runtime download is incomplete: expected {manifest.ArchiveSize} bytes, received {copied}.");
    }

    private static async Task VerifyArchiveAsync(
        string path,
        ConversionRuntimeManifest manifest,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 256, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        if (!string.Equals(actual, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Conversion runtime SHA-256 verification failed.");
    }

    private static void ExtractArchiveSafely(string archivePath, string destinationRoot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationRoot);
        var root = Path.GetFullPath(destinationRoot) + Path.DirectorySeparatorChar;
        using var stream = File.OpenRead(archivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name)) continue;

            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(destinationRoot, relative));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Conversion runtime archive contains an unsafe path.");

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = entry.Open();
            using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    private static async Task ValidateExecutableAsync(
        string executablePath,
        string calibreVersion,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("--version");

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidDataException("The downloaded conversion runtime could not be started.");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = string.Concat(await stdout, " ", await stderr);
        if (process.ExitCode != 0 || !output.Contains($"calibre {calibreVersion}", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded conversion runtime failed its executable validation.");
    }

    private IEnumerable<InstalledRuntime> FindInstalledRuntimes()
    {
        if (!Directory.Exists(AppPaths.ConversionRuntimesRoot))
            yield break;

        foreach (var directory in Directory.EnumerateDirectories(AppPaths.ConversionRuntimesRoot))
        {
            if (Path.GetFileName(directory).StartsWith(".", StringComparison.Ordinal)) continue;

            var platformRoot = Path.Combine(directory, "win-x64");
            var manifestPath = Path.Combine(platformRoot, "pagearc-runtime-manifest.json");
            if (!File.Exists(manifestPath)) continue;

            ConversionRuntimeManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<ConversionRuntimeManifest>(
                    File.ReadAllText(manifestPath),
                    JsonOptions);
                if (manifest is null) continue;
                ValidateManifest(manifest, requirePinnedIdentity: false);
                if (!IsCompatibleWithCurrentPageArc(manifest.MinimumPageArcVersion)) continue;
            }
            catch
            {
                continue;
            }

            var executable = Path.Combine(
                platformRoot,
                manifest.ExecutableRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(executable) || new FileInfo(executable).Length == 0) continue;

            long bytes = 0;
            try
            {
                bytes = Directory.EnumerateFiles(platformRoot, "*", SearchOption.AllDirectories)
                    .Sum(path => new FileInfo(path).Length);
            }
            catch { }

            yield return new InstalledRuntime(manifest, executable, bytes);
        }
    }

    private string GetInstallRoot(string packageVersion) =>
        Path.Combine(AppPaths.ConversionRuntimesRoot, packageVersion, "win-x64");

    private void RemoveOtherRuntimeVersions(string keepPackageVersion)
    {
        if (!Directory.Exists(AppPaths.ConversionRuntimesRoot)) return;
        foreach (var directory in Directory.EnumerateDirectories(AppPaths.ConversionRuntimesRoot))
        {
            var name = Path.GetFileName(directory);
            if (name.StartsWith(".", StringComparison.Ordinal) || string.Equals(name, keepPackageVersion, StringComparison.Ordinal))
                continue;
            TryDeleteDirectory(directory);
        }
    }

    private void PublishOperationState(
        ConversionRuntimeOperationState state,
        IProgress<ConversionRuntimeProgress>? externalProgress = null)
    {
        lock (_operationStateGate)
            _operationState = state;

        OperationStateChanged?.Invoke(this, state);
        if (state.Stage is not "idle" and not "failed")
        {
            externalProgress?.Report(new ConversionRuntimeProgress(
                state.Stage,
                state.BytesTransferred,
                state.TotalBytes));
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.UserAgent.ParseAdd("PageArc/1.4");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        return request;
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }

    private sealed record InstalledRuntime(
        ConversionRuntimeManifest Manifest,
        string ExecutablePath,
        long Bytes);

    private sealed class GitHubReleaseEnvelope
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;
        [JsonPropertyName("draft")]
        public bool Draft { get; set; }
        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }
        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("size")]
        public long Size { get; set; }
        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }

    private sealed class RuntimePackageVersionComparer : IComparer<string>
    {
        public static RuntimePackageVersionComparer Instance { get; } = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            if (!TryParse(x, out var xv, out var xr)) return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            if (!TryParse(y, out var yv, out var yr)) return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            var versionCompare = xv.CompareTo(yv);
            return versionCompare != 0 ? versionCompare : xr.CompareTo(yr);
        }

        public static bool TryParse(string value, out Version calibre, out int revision)
        {
            calibre = new Version(0, 0);
            revision = 0;
            const string marker = "-pagearc.";
            var index = value.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index <= 0) return false;
            if (!Version.TryParse(value[..index], out calibre!)) return false;
            return int.TryParse(value[(index + marker.Length)..], out revision) && revision >= 0;
        }
    }
}
