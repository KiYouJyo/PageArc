using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using PageArc.Models;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Services.Store;
using Windows.Storage;

namespace PageArc.Services;

public sealed class GitHubUpdateService
{
    public const string ExpectedSignerSubject = "CN=AppPublisher";
    public const string ExpectedSignerThumbprint = "BD85AD77A651C86CA01A480C8E9BC64952993F98";
    public static readonly Uri LatestReleaseApi = new("https://api.github.com/repos/KiYouJyo/PageArc/releases/latest");

    private static readonly HttpClient Client = CreateClient();
    private readonly IMsixPackageSignatureVerifier _signatureVerifier = new MsixPackageSignatureVerifier();
    private StoreContext? _storeContext;
    private IReadOnlyList<StorePackageUpdate> _pendingStoreUpdates = [];
    private string? _pendingInstallerPath;
    private string? _pendingReleaseTag;

    private static string UpdateCacheRoot => Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Updates");
    private static string PendingStatePath => Path.Combine(UpdateCacheRoot, "github-pending-update.json");

    public UpdateCheckResult? LastResult { get; private set; }

    public bool IsGitHubUpdateReadyToInstall =>
        !DistributionChannel.IsStore &&
        !string.IsNullOrWhiteSpace(_pendingInstallerPath) &&
        File.Exists(_pendingInstallerPath);

    public void InitializeForWindow(Window window)
    {
        if (!DistributionChannel.IsStore) return;
        _storeContext = StoreContext.GetDefault();
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(_storeContext, windowHandle);
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        LastResult = await CheckForUpdatesCoreAsync(cancellationToken);
        if (!DistributionChannel.IsStore)
        {
            if (LastResult.Status == UpdateCheckStatus.UpdateAvailable)
                LoadPendingState(LastResult);
            else
                ClearPendingState(deletePackage: true);
        }
        return LastResult;
    }

    private async Task<UpdateCheckResult> CheckForUpdatesCoreAsync(CancellationToken cancellationToken)
    {
        var localVersion = GetCurrentVersion();
        if (DistributionChannel.IsStore)
            return await CheckStoreUpdatesAsync(localVersion);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd($"PageArc/{localVersion.Major}.{localVersion.Minor}.{localVersion.Build}");
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(UpdateCheckStatus.NoRelease, localVersion);
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) && values.Contains("0"))
                return new(UpdateCheckStatus.RateLimited, localVersion);
            if (!response.IsSuccessStatusCode)
                return new(UpdateCheckStatus.RequestFailed, localVersion);

            var payload = await response.Content.ReadFromJsonAsync<ReleasePayload>(cancellationToken: cancellationToken);
            if (payload is null ||
                !VersionParser.TryParseTag(payload.TagName, out var remoteVersion) ||
                string.IsNullOrWhiteSpace(payload.TagName) ||
                string.IsNullOrWhiteSpace(payload.HtmlUrl) ||
                !Uri.TryCreate(payload.HtmlUrl, UriKind.Absolute, out var releaseUri))
                return new(UpdateCheckStatus.InvalidResponse, localVersion);

            var installer = SelectInstaller(payload.Assets);
            var checksum = SelectChecksum(payload.Assets);
            return remoteVersion.CompareTo(VersionParser.Normalize(localVersion)) > 0
                ? new(
                    UpdateCheckStatus.UpdateAvailable,
                    localVersion,
                    remoteVersion,
                    releaseUri,
                    payload.Name,
                    payload.Body,
                    installer?.Uri,
                    installer?.Name,
                    installer?.Size ?? 0,
                    payload.TagName,
                    checksum?.Uri)
                : new(UpdateCheckStatus.UpToDate, localVersion, remoteVersion, releaseUri, payload.Name, payload.Body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(UpdateCheckStatus.TimedOut, localVersion);
        }
        catch (HttpRequestException)
        {
            return new(UpdateCheckStatus.ConnectionFailed, localVersion);
        }
        catch (JsonException)
        {
            return new(UpdateCheckStatus.InvalidResponse, localVersion);
        }
    }

    public async Task<StorageFile> DownloadInstallerAsync(
        UpdateCheckResult update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (DistributionChannel.IsStore)
            throw new InvalidOperationException("Microsoft Store builds are updated by the Store.");
        if (update.InstallerUri is null || string.IsNullOrWhiteSpace(update.InstallerName))
            throw new InvalidOperationException("The release does not contain a compatible Windows installer asset.");

        Directory.CreateDirectory(UpdateCacheRoot);
        var safeName = Path.GetFileName(update.InstallerName);
        var packagePath = Path.Combine(UpdateCacheRoot, safeName);
        var file = await StorageFile.GetFileFromPathAsync(await EnsurePackageFileAsync(packagePath));

        using var request = new HttpRequestMessage(HttpMethod.Get, update.InstallerUri);
        request.Headers.UserAgent.ParseAdd($"PageArc/{update.LocalVersion.Major}.{update.LocalVersion.Minor}.{update.LocalVersion.Build}");
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, useAsync: true);
        var buffer = new byte[128 * 1024];
        long received = 0;
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            received += count;
            if (total is > 0) progress?.Report(received * 100d / total.Value);
        }
        progress?.Report(100);
        return file;
    }

    /// <summary>
    /// GitHub channel phase 1: download the package, verify SHA-256 and the pinned
    /// signing certificate, then persist the verified package for the explicit
    /// "Restart to update" action. No registration is attempted while PageArc is running.
    /// </summary>
    public async Task<UpdateInstallResult> DownloadAndPrepareAsync(
        UpdateCheckResult update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (DistributionChannel.IsStore)
            return await InstallStoreUpdatesAsync(progress, cancellationToken);
        if (update.InstallerUri is null || string.IsNullOrWhiteSpace(update.InstallerName) ||
            update.ChecksumUri is null || string.IsNullOrWhiteSpace(update.ReleaseTag))
            return new(UpdateInstallStatus.Failed, "The release is missing its MSIX package or SHA256SUMS.txt.");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expectedHash = await DownloadExpectedHashAsync(update, cancellationToken);
            if (expectedHash is null)
                return new(UpdateInstallStatus.Failed, "SHA256SUMS.txt does not contain the selected package hash.");

            var downloadProgress = progress is null
                ? null
                : new Progress<double>(value => progress.Report(value * 0.85d));
            var file = await DownloadInstallerAsync(update, downloadProgress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(88);
            string actualHash;
            await using (var stream = File.OpenRead(file.Path))
                actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(file.Path);
                return new(UpdateInstallStatus.Failed, "The downloaded package failed SHA-256 verification.");
            }

            progress?.Report(94);
            var signature = _signatureVerifier.Verify(file.Path);
            if (!signature.IsValid ||
                !ExpectedSignerSubject.Equals(signature.SignerSubject, StringComparison.Ordinal) ||
                !ExpectedSignerThumbprint.Equals(signature.SignerThumbprint, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(file.Path);
                var reason = !signature.IsValid
                    ? signature.FailureCode
                    : !ExpectedSignerSubject.Equals(signature.SignerSubject, StringComparison.Ordinal)
                        ? "SignerSubjectMismatch"
                        : "SignerThumbprintMismatch";
                return new(UpdateInstallStatus.Failed, $"Package signature verification failed: {reason}.");
            }

            if (!string.Equals(_pendingInstallerPath, file.Path, StringComparison.OrdinalIgnoreCase))
                TryDeleteFile(_pendingInstallerPath);
            _pendingInstallerPath = file.Path;
            _pendingReleaseTag = update.ReleaseTag;
            SavePendingState(update.ReleaseTag, file.Path);
            progress?.Report(100);
            return new(UpdateInstallStatus.RestartRequired);
        }
        catch (OperationCanceledException)
        {
            return new(UpdateInstallStatus.Canceled);
        }
        catch (Exception ex)
        {
            return new(UpdateInstallStatus.Failed, ex.Message);
        }
    }

    /// <summary>
    /// GitHub channel phase 2. This deliberately mirrors SpatialViewer: Windows
    /// PackageManager owns shutdown of the in-use old package, while
    /// RegisterApplicationRestart asks Windows to launch the newly registered package.
    /// This avoids the previous race between deferred registration and AppInstance.Restart.
    /// </summary>
    public async Task<UpdateInstallResult> InstallPreparedUpdateAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (DistributionChannel.IsStore)
            return new(UpdateInstallStatus.Failed, "Microsoft Store updates are installed through StoreContext.");

        if (!IsGitHubUpdateReadyToInstall)
        {
            if (LastResult is { Status: UpdateCheckStatus.UpdateAvailable } update)
                LoadPendingState(update);
            if (!IsGitHubUpdateReadyToInstall)
                return new(UpdateInstallStatus.Failed, "No verified update is ready to install.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var packagePath = _pendingInstallerPath!;
            var packageInfo = new FileInfo(packagePath);
            Debug.WriteLine($"PageArc update deployment starting: Package={packagePath}; Bytes={packageInfo.Length}; Current={Package.Current.Id.FullName}");

            using var restart = ApplicationRestartRegistration.Register(out var restartHresult);
            Debug.WriteLine($"RegisterApplicationRestart HRESULT=0x{restartHresult:X8}");

            var manager = new PackageManager();
            var operation = manager.AddPackageAsync(new Uri(packagePath), null, DeploymentOptions.ForceApplicationShutdown);
            operation.Progress = (_, value) =>
            {
                if (value.percentage is >= 0 and <= 100)
                    progress?.Report(value.percentage);
            };

            var deployment = await operation;
            Debug.WriteLine($"PageArc update deployment returned: Registered={deployment.IsRegistered}; Error={deployment.ExtendedErrorCode}; Text={deployment.ErrorText}");
            if (!deployment.IsRegistered || deployment.ExtendedErrorCode is { } error && error != default)
                return new(UpdateInstallStatus.Failed, deployment.ErrorText ?? error?.Message ?? "Windows package deployment failed.");

            ClearPendingState(deletePackage: true);
            progress?.Report(100);
            return new(UpdateInstallStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            return new(UpdateInstallStatus.Canceled);
        }
        catch (COMException ex)
        {
            return new(UpdateInstallStatus.Failed, $"0x{ex.HResult:X8}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new(UpdateInstallStatus.Failed, ex.Message);
        }
    }

    // Backward-compatible entry point retained for Store and older callers. On the
    // GitHub channel it now performs only the verified preparation phase.
    public Task<UpdateInstallResult> DownloadAndInstallAsync(
        UpdateCheckResult update,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default) =>
        DistributionChannel.IsStore
            ? InstallStoreUpdatesAsync(progress, cancellationToken)
            : DownloadAndPrepareAsync(update, progress, cancellationToken);

    private async Task<string?> DownloadExpectedHashAsync(UpdateCheckResult update, CancellationToken cancellationToken)
    {
        if (update.ChecksumUri is null || string.IsNullOrWhiteSpace(update.InstallerName)) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, update.ChecksumUri);
        request.Headers.UserAgent.ParseAdd($"PageArc/{update.LocalVersion.Major}.{update.LocalVersion.Minor}.{update.LocalVersion.Build}");
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var checksumText = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseChecksum(checksumText, update.InstallerName);
    }

    private async Task<UpdateCheckResult> CheckStoreUpdatesAsync(Version localVersion)
    {
        if (_storeContext is null)
            return new(UpdateCheckStatus.RequestFailed, localVersion);
        try
        {
            _pendingStoreUpdates = await _storeContext.GetAppAndOptionalStorePackageUpdatesAsync();
            if (_pendingStoreUpdates.Count == 0)
                return new(UpdateCheckStatus.UpToDate, localVersion, localVersion);

            var packageVersion = _pendingStoreUpdates
                .Select(item => item.Package.Id.Version)
                .Select(value => new Version(value.Major, value.Minor, value.Build, value.Revision))
                .OrderByDescending(value => value)
                .First();
            return new(UpdateCheckStatus.UpdateAvailable, localVersion, packageVersion);
        }
        catch
        {
            _pendingStoreUpdates = [];
            return new(UpdateCheckStatus.RequestFailed, localVersion);
        }
    }

    private async Task<UpdateInstallResult> InstallStoreUpdatesAsync(
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (_storeContext is null || _pendingStoreUpdates.Count == 0)
            return new(UpdateInstallStatus.Failed, "No Microsoft Store update is ready to install.");
        try
        {
            var operation = _storeContext.RequestDownloadAndInstallStorePackageUpdatesAsync(_pendingStoreUpdates);
            operation.Progress = (_, status) => progress?.Report(status.TotalDownloadProgress * 100d);
            using var registration = cancellationToken.Register(operation.Cancel);
            var result = await operation;
            progress?.Report(100);
            return result.OverallState switch
            {
                StorePackageUpdateState.Completed => new(UpdateInstallStatus.Completed),
                StorePackageUpdateState.Canceled => new(UpdateInstallStatus.Canceled),
                _ => new(UpdateInstallStatus.Failed, result.OverallState.ToString())
            };
        }
        catch (OperationCanceledException)
        {
            return new(UpdateInstallStatus.Canceled);
        }
        catch (Exception ex)
        {
            return new(UpdateInstallStatus.Failed, ex.Message);
        }
    }

    private static Version GetCurrentVersion()
    {
        try
        {
            var packageVersion = Package.Current.Id.Version;
            return VersionParser.Normalize(new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision));
        }
        catch
        {
            var assemblyVersion = typeof(App).Assembly.GetName().Version;
            return VersionParser.Normalize(assemblyVersion ?? new Version(0, 1, 0));
        }
    }

    private static ReleaseAsset? SelectInstaller(IReadOnlyList<ReleaseAssetPayload>? assets)
    {
        return assets?
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Name)
                && !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl)
                && Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out _))
            .Select(asset => new ReleaseAsset(
                asset.Name!, new Uri(asset.BrowserDownloadUrl!), asset.Size,
                InstallerPriority(asset.Name!)))
            .Where(asset => asset.Priority < int.MaxValue)
            .OrderBy(asset => asset.Priority)
            .ThenByDescending(asset => asset.Size)
            .FirstOrDefault();
    }

    private static ReleaseAsset? SelectChecksum(IReadOnlyList<ReleaseAssetPayload>? assets)
    {
        var checksum = assets?.SingleOrDefault(asset =>
            string.Equals(asset.Name, "SHA256SUMS.txt", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl) &&
            Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out _));
        return checksum is null
            ? null
            : new ReleaseAsset(checksum.Name!, new Uri(checksum.BrowserDownloadUrl!), checksum.Size, 0);
    }

    private static int InstallerPriority(string name)
    {
        var lower = name.ToLowerInvariant();
        // Direct package assets can be deployed by PackageManager without leaving PageArc.
        // Do not select .appinstaller: PackageManager only gained support for it after
        // PageArc's Windows 10 2004 minimum, and it can reference external package URLs.
        if (lower.EndsWith(".msixbundle", StringComparison.Ordinal)) return 0;
        if (lower.Contains("x64", StringComparison.Ordinal) && lower.EndsWith(".msix", StringComparison.Ordinal)) return 1;
        if (lower.EndsWith(".msix", StringComparison.Ordinal)) return 2;
        return int.MaxValue;
    }

    private static string? ParseChecksum(string content, string fileName)
    {
        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOfAny([' ', '\t']);
            if (separator <= 0) continue;
            var hash = line[..separator].Trim();
            var name = line[separator..].Trim().TrimStart('*');
            if (hash.Length == 64 && name.Equals(fileName, StringComparison.Ordinal) && hash.All(Uri.IsHexDigit))
                return hash.ToUpperInvariant();
        }
        return null;
    }

    private void LoadPendingState(UpdateCheckResult update)
    {
        _pendingInstallerPath = null;
        _pendingReleaseTag = null;
        try
        {
            if (!File.Exists(PendingStatePath)) return;
            var state = JsonSerializer.Deserialize<PendingUpdateState>(File.ReadAllText(PendingStatePath));
            if (state is null ||
                !string.Equals(state.ReleaseTag, update.ReleaseTag, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(state.PackagePath) ||
                !File.Exists(state.PackagePath))
                return;
            _pendingInstallerPath = state.PackagePath;
            _pendingReleaseTag = state.ReleaseTag;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PageArc pending update state load failed: {ex.Message}");
        }
    }

    private static void SavePendingState(string releaseTag, string packagePath)
    {
        Directory.CreateDirectory(UpdateCacheRoot);
        File.WriteAllText(PendingStatePath, JsonSerializer.Serialize(new PendingUpdateState(releaseTag, packagePath)));
    }

    private void ClearPendingState(bool deletePackage)
    {
        var packagePath = _pendingInstallerPath;
        _pendingInstallerPath = null;
        _pendingReleaseTag = null;
        if (deletePackage) TryDeleteFile(packagePath);
        try
        {
            if (File.Exists(PendingStatePath)) File.Delete(PendingStatePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PageArc pending update state cleanup failed: {ex.Message}");
        }
    }

    private static async Task<string> EnsurePackageFileAsync(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path))
            await File.WriteAllBytesAsync(path, []);
        return path;
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }

    private static HttpClient CreateClient() => new() { Timeout = TimeSpan.FromMinutes(10) };

    private sealed record ReleasePayload(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("assets")] IReadOnlyList<ReleaseAssetPayload>? Assets);

    private sealed record ReleaseAssetPayload(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl,
        [property: JsonPropertyName("size")] long Size);

    private sealed record ReleaseAsset(string Name, Uri Uri, long Size, int Priority);
    private sealed record PendingUpdateState(string ReleaseTag, string PackagePath);
}

internal static class ApplicationRestartRegistration
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterApplicationRestart(string? commandLine, uint flags);

    public static IDisposable Register(out int hresult)
    {
        hresult = RegisterApplicationRestart(null, 0);
        if (hresult != 0) Marshal.ThrowExceptionForHR(hresult);
        return new Registration();
    }

    private sealed class Registration : IDisposable
    {
        public void Dispose()
        {
            var result = RegisterApplicationRestart(string.Empty, 0);
            if (result != 0) Debug.WriteLine($"RegisterApplicationRestart cleanup returned 0x{result:X8}.");
        }
    }
}
