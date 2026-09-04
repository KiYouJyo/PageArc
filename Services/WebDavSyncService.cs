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
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    public async Task TestConnectionAsync(
        WebDavConnectionSettings settings,
        string password,
        CancellationToken cancellationToken = default)
    {
        var collectionUri = settings.GetCollectionUri();

        using (var request = CreateRequest(new HttpMethod("PROPFIND"), collectionUri, settings, password))
        {
            request.Headers.TryAddWithoutValidation("Depth", "0");
            request.Content = new StringContent(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><d:propfind xmlns:d=\"DAV:\"><d:prop><d:resourcetype/></d:prop></d:propfind>",
                Encoding.UTF8,
                "application/xml");

            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (IsWebDavSuccess(response)) return;

            if (response.StatusCode is not HttpStatusCode.MethodNotAllowed
                && response.StatusCode != HttpStatusCode.NotImplemented)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        using var optionsRequest = CreateRequest(HttpMethod.Options, collectionUri, settings, password);
        using var optionsResponse = await _client.SendAsync(optionsRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        optionsResponse.EnsureSuccessStatusCode();
    }

    public async Task<bool> DownloadFileAsync(
        WebDavConnectionSettings settings,
        string password,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path is required.", nameof(destinationPath));

        using var request = CreateRequest(HttpMethod.Get, settings.GetEndpointUri(), settings, password);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temp = fullPath + ".tmp";
        try
        {
            {
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var target = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, useAsync: true);
                await source.CopyToAsync(target, cancellationToken);
                await target.FlushAsync(cancellationToken);
            }
            File.Move(temp, fullPath, true);
            return true;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    public async Task UploadFileAsync(
        WebDavConnectionSettings settings,
        string password,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path is required.", nameof(sourcePath));

        await using var source = new FileStream(Path.GetFullPath(sourcePath), FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, useAsync: true);
        using var request = CreateRequest(HttpMethod.Put, settings.GetEndpointUri(), settings, password);
        request.Content = new StreamContent(source, 1024 * 128);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PageArcReadingBackup?> DownloadAsync(
        WebDavConnectionSettings settings,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, settings.GetEndpointUri(), settings, password);
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
        using var request = CreateRequest(HttpMethod.Put, settings.GetEndpointUri(), settings, password);
        request.Content = new StringContent(ReadingBackupService.Serialize(backup), Encoding.UTF8, "application/json");
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static bool IsWebDavSuccess(HttpResponseMessage response) =>
        response.IsSuccessStatusCode || (int)response.StatusCode == 207;

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        Uri uri,
        WebDavConnectionSettings settings,
        string password)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
        request.Headers.UserAgent.ParseAdd("PageArc/1.2");
        return request;
    }
}
