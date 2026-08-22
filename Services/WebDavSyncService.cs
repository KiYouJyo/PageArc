using System.Net;
using System.Net.Http.Headers;
using System.Text;
using PageArc.Models;

namespace PageArc.Services;

public sealed class WebDavSyncService
{
    private readonly HttpClient _client;

    public WebDavSyncService(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task TestConnectionAsync(
        WebDavConnectionSettings settings,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Head, settings, password);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // A missing sync file still proves that the WebDAV endpoint and credentials are reachable.
            return;
        }
        if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
        {
            using var optionsRequest = CreateRequest(HttpMethod.Options, settings, password);
            using var optionsResponse = await _client.SendAsync(optionsRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            optionsResponse.EnsureSuccessStatusCode();
            return;
        }
        response.EnsureSuccessStatusCode();
    }

    public async Task<PageArcReadingBackup?> DownloadAsync(
        WebDavConnectionSettings settings,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, settings, password);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return ReadingBackupService.Deserialize(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task UploadAsync(
        WebDavConnectionSettings settings,
        string password,
        PageArcReadingBackup backup,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, settings, password);
        request.Content = new StringContent(ReadingBackupService.Serialize(backup), Encoding.UTF8, "application/json");
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        WebDavConnectionSettings settings,
        string password)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var request = new HttpRequestMessage(method, settings.GetEndpointUri());
        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
        request.Headers.UserAgent.ParseAdd("PageArc/1.0");
        return request;
    }
}
