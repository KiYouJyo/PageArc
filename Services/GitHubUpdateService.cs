using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PageArc.Models;

namespace PageArc.Services;

public sealed class GitHubUpdateService
{
    public static readonly Uri LatestReleaseApi = new("https://api.github.com/repos/KiYouJyo/PageArc/releases/latest");
    private static readonly HttpClient Client = CreateClient();

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var localVersion = GetCurrentVersion();
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

            return remoteVersion.CompareTo(VersionParser.Normalize(localVersion)) > 0
                ? new(UpdateCheckStatus.UpdateAvailable, localVersion, remoteVersion, releaseUri, payload.Name, payload.Body)
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

    private static Version GetCurrentVersion()
    {
        var assemblyVersion = typeof(App).Assembly.GetName().Version;
        return VersionParser.Normalize(assemblyVersion ?? new Version(0, 1, 0));
    }

    private static HttpClient CreateClient() => new() { Timeout = TimeSpan.FromSeconds(15) };

    private sealed record ReleasePayload(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("html_url")] string? HtmlUrl);
}
