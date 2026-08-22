using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PageArc.Models;
using Windows.Storage;

namespace PageArc.Services;

public sealed class GitHubUpdateService
{
    public static readonly Uri LatestReleaseApi = new("https://api.github.com/repos/KiYouJyo/PageArc/releases/latest");
    private static readonly HttpClient Client = CreateClient();

    public UpdateCheckResult? LastResult { get; private set; }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        LastResult = await CheckForUpdatesCoreAsync(cancellationToken);
        return LastResult;
    }

    private async Task<UpdateCheckResult> CheckForUpdatesCoreAsync(CancellationToken cancellationToken)
    {
        var localVersion = GetCurrentVersion();
        if (DistributionChannel.IsStore)
            return new(UpdateCheckStatus.StoreManaged, localVersion);
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
                string.IsNullOrWhiteSpace(payload.HtmlUrl) ||
                !Uri.TryCreate(payload.HtmlUrl, UriKind.Absolute, out var releaseUri))
                return new(UpdateCheckStatus.InvalidResponse, localVersion);

            var installer = SelectInstaller(payload.Assets);
            return remoteVersion.CompareTo(VersionParser.Normalize(localVersion)) > 0
                ? new(UpdateCheckStatus.UpdateAvailable, localVersion, remoteVersion, releaseUri, payload.Name, payload.Body,
                    installer?.Uri, installer?.Name, installer?.Size ?? 0)
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

        var safeName = Path.GetFileName(update.InstallerName);
        var file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(safeName, CreationCollisionOption.ReplaceExisting);
        using var request = new HttpRequestMessage(HttpMethod.Get, update.InstallerUri);
        request.Headers.UserAgent.ParseAdd($"PageArc/{update.LocalVersion.Major}.{update.LocalVersion.Minor}.{update.LocalVersion.Build}");
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = await file.OpenStreamForWriteAsync();
        destination.SetLength(0);
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

    private static Version GetCurrentVersion()
    {
        var assemblyVersion = typeof(App).Assembly.GetName().Version;
        return VersionParser.Normalize(assemblyVersion ?? new Version(0, 1, 0));
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

    private static int InstallerPriority(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.EndsWith(".appinstaller", StringComparison.Ordinal)) return 0;
        if (lower.EndsWith(".msixbundle", StringComparison.Ordinal)) return 1;
        if (lower.Contains("x64", StringComparison.Ordinal) && lower.EndsWith(".msix", StringComparison.Ordinal)) return 2;
        if (lower.EndsWith(".msix", StringComparison.Ordinal)) return 3;
        return int.MaxValue;
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
}
