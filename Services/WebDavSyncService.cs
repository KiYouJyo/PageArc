using System.Net;
using System.Net.Http.Headers;
using System.Text;
using PageArc.Models;

namespace PageArc.Services;

public sealed class WebDavSyncService
{
    private static readonly HttpMethod PropFindMethod = new("PROPFIND");
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

        using (var request = CreateRequest(PropFindMethod, collectionUri, settings, password))
        {
            request.Headers.TryAddWithoutValidation("Depth", "0");
            request.Content = CreatePropFindContent(includeListingFields: false);

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

    public async Task<WebDavArchiveListResult> ListArchivesAsync(
        WebDavConnectionSettings settings,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            using var request = CreateRequest(PropFindMethod, settings.GetCollectionUri(), settings, password);
            request.Headers.TryAddWithoutValidation("Depth", "1");
            request.Content = CreatePropFindContent(includeListingFields: true);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);

            if ((int)response.StatusCode == 207 || response.IsSuccessStatusCode)
            {
                var xml = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(xml))
                {
                    var items = WebDavArchiveListingParser.Parse(xml);
                    if (items.Count > 0 || !settings.UsesDirectArchiveUrl)
                        return new(items, true);
                }

                if (!settings.UsesDirectArchiveUrl)
                    return new([], true);
            }

            if (settings.UsesDirectArchiveUrl
                && response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
            {
                return await ReadDirectArchiveMetadataAsync(settings, password, cancellationToken);
            }

            return new([], false, $"HTTP {(int)response.StatusCode}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new([], false, "Timeout");
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Xml.XmlException or FormatException)
        {
            if (settings.UsesDirectArchiveUrl)
            {
                try { return await ReadDirectArchiveMetadataAsync(settings, password, cancellationToken); }
                catch { }
            }
            return new([], false, ex.GetType().Name);
        }
    }

    public Task<bool> DownloadArchiveAsync(
        WebDavConnectionSettings settings,
        string password,
        WebDavArchiveItem archive,
        string destinationPath,
        IProgress<WebDavTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);
        return DownloadUriAsync(
            settings.GetArchiveUri(archive.FileName),
            settings,
            password,
            destinationPath,
            archive.Size > 0 ? archive.Size : null,
            progress,
            cancellationToken);
    }

    public async Task UploadArchiveAsync(
        WebDavConnectionSettings settings,
        string password,
        string sourcePath,
        string fileName,
        IProgress<WebDavTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        if (!WebDavArchiveItem.IsPageArcBackupFileName(fileName))
            throw new ArgumentException("A valid PageArc archive filename is required.", nameof(fileName));

        var fullPath = Path.GetFullPath(sourcePath);
        var info = new FileInfo(fullPath);
        if (!info.Exists) throw new FileNotFoundException("Upload source does not exist.", fullPath);

        var source = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, useAsync: true);
        using var request = CreateRequest(HttpMethod.Put, settings.GetUploadUri(fileName), settings, password);
        request.Content = new ProgressStreamContent(source, info.Length, progress, cancellationToken);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        progress?.Report(new WebDavTransferProgress(info.Length, info.Length));
    }

    public async Task DeleteArchiveAsync(
        WebDavConnectionSettings settings,
        string password,
        WebDavArchiveItem archive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);
        using var request = CreateRequest(HttpMethod.Delete, settings.GetArchiveUri(archive.FileName), settings, password);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return;
        response.EnsureSuccessStatusCode();
    }

    public Task<bool> DownloadFileAsync(
        WebDavConnectionSettings settings,
        string password,
        string destinationPath,
        CancellationToken cancellationToken = default) =>
        DownloadUriAsync(settings.GetEndpointUri(), settings, password, destinationPath, null, null, cancellationToken);

    public async Task UploadFileAsync(
        WebDavConnectionSettings settings,
        string password,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path is required.", nameof(sourcePath));

        var fullPath = Path.GetFullPath(sourcePath);
        var info = new FileInfo(fullPath);
        var source = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, useAsync: true);
        using var request = CreateRequest(HttpMethod.Put, settings.GetEndpointUri(), settings, password);
        request.Content = new ProgressStreamContent(source, info.Length, null, cancellationToken);
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

    private async Task<bool> DownloadUriAsync(
        Uri uri,
        WebDavConnectionSettings settings,
        string password,
        string destinationPath,
        long? expectedLength,
        IProgress<WebDavTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path is required.", nameof(destinationPath));

        using var request = CreateRequest(HttpMethod.Get, uri, settings, password);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temp = fullPath + ".tmp";
        var total = response.Content.Headers.ContentLength ?? expectedLength;

        try
        {
            {
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var target = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, useAsync: true);
                var buffer = new byte[1024 * 128];
                long copied = 0;
                while (true)
                {
                    var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read == 0) break;
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    copied += read;
                    progress?.Report(new WebDavTransferProgress(copied, total));
                }
                await target.FlushAsync(cancellationToken);
                progress?.Report(new WebDavTransferProgress(copied, total ?? copied));
            }

            File.Move(temp, fullPath, true);
            return true;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private async Task<WebDavArchiveListResult> ReadDirectArchiveMetadataAsync(
        WebDavConnectionSettings settings,
        string password,
        CancellationToken cancellationToken)
    {
        var fileName = settings.GetDirectArchiveFileName();
        if (!WebDavArchiveItem.IsPageArcBackupFileName(fileName))
            return new([], true);

        using var request = CreateRequest(HttpMethod.Head, settings.GetEndpointUri(), settings, password);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new([], true);
        response.EnsureSuccessStatusCode();

        var size = response.Content.Headers.ContentLength ?? 0;
        var modified = response.Content.Headers.LastModified?.ToUniversalTime();
        DateTimeOffset? created = null;
        string? version = null;
        if (WebDavArchiveItem.TryParseFileName(fileName!, out var parsedCreated, out var parsedVersion))
        {
            created = parsedCreated;
            version = parsedVersion;
        }
        return new([new WebDavArchiveItem(fileName!, size, created, modified, version)], true);
    }

    private static StringContent CreatePropFindContent(bool includeListingFields)
    {
        var properties = includeListingFields
            ? "<d:displayname/><d:getcontentlength/><d:getlastmodified/><d:resourcetype/>"
            : "<d:resourcetype/>";
        return new StringContent(
            $"<?xml version=\"1.0\" encoding=\"utf-8\"?><d:propfind xmlns:d=\"DAV:\"><d:prop>{properties}</d:prop></d:propfind>",
            Encoding.UTF8,
            "application/xml");
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
        request.Headers.UserAgent.ParseAdd("PageArc/1.3.1");
        return request;
    }

    private sealed class ProgressStreamContent : HttpContent
    {
        private readonly Stream _source;
        private readonly long _length;
        private readonly IProgress<WebDavTransferProgress>? _progress;
        private readonly CancellationToken _cancellationToken;

        public ProgressStreamContent(
            Stream source,
            long length,
            IProgress<WebDavTransferProgress>? progress,
            CancellationToken cancellationToken)
        {
            _source = source;
            _length = length;
            _progress = progress;
            _cancellationToken = cancellationToken;
            Headers.ContentLength = length;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _length;
            return true;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[1024 * 128];
            long copied = 0;
            while (true)
            {
                var read = await _source.ReadAsync(buffer.AsMemory(), _cancellationToken);
                if (read == 0) break;
                await stream.WriteAsync(buffer.AsMemory(0, read), _cancellationToken);
                copied += read;
                _progress?.Report(new WebDavTransferProgress(copied, _length));
            }
            _progress?.Report(new WebDavTransferProgress(_length, _length));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _source.Dispose();
            base.Dispose(disposing);
        }
    }
}
