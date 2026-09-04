using System.Net;
using System.Text;
using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class WebDavArchiveServiceTests
{
    [Fact]
    public async Task ArchiveService_ListsTransfersProgressAndDeletesVersionedArchives()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-webdav-archives-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var handler = new ArchiveWebDavHandler();
            using var client = new HttpClient(handler);
            var service = new WebDavSyncService(client);
            var settings = new WebDavConnectionSettings("https://example.com/dav/PageArc/", "reader");

            var sourcePath = Path.Combine(root, "source.pagearcbackup");
            var destinationPath = Path.Combine(root, "download.pagearcbackup");
            var bytes = Enumerable.Range(0, 512 * 1024).Select(i => (byte)(i % 251)).ToArray();
            await File.WriteAllBytesAsync(sourcePath, bytes);

            var fileName = WebDavArchiveItem.CreateFileName(
                new DateTimeOffset(2026, 9, 4, 8, 30, 0, TimeSpan.Zero),
                "1.3.1");

            var uploadProgress = new List<WebDavTransferProgress>();
            await service.UploadArchiveAsync(
                settings,
                "secret",
                sourcePath,
                fileName,
                new ImmediateProgress<WebDavTransferProgress>(uploadProgress.Add));

            Assert.NotEmpty(uploadProgress);
            Assert.Equal(bytes.Length, uploadProgress[^1].BytesTransferred);
            Assert.Equal(bytes.Length, uploadProgress[^1].TotalBytes);

            var listing = await service.ListArchivesAsync(settings, "secret");
            Assert.True(listing.Succeeded, listing.ErrorCode);
            var archive = Assert.Single(listing.Items);
            Assert.Equal(fileName, archive.FileName);
            Assert.Equal(bytes.Length, archive.Size);
            Assert.Equal("1.3.1", archive.AppVersion);

            var downloadProgress = new List<WebDavTransferProgress>();
            Assert.True(await service.DownloadArchiveAsync(
                settings,
                "secret",
                archive,
                destinationPath,
                new ImmediateProgress<WebDavTransferProgress>(downloadProgress.Add)));

            Assert.NotEmpty(downloadProgress);
            Assert.Equal(bytes.Length, downloadProgress[^1].BytesTransferred);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(destinationPath));

            await service.DeleteArchiveAsync(settings, "secret", archive);
            listing = await service.ListArchivesAsync(settings, "secret");
            Assert.True(listing.Succeeded, listing.ErrorCode);
            Assert.Empty(listing.Items);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class ImmediateProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private sealed class ArchiveWebDavHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (byte[] Payload, DateTimeOffset Modified)> _files =
            new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var method = request.Method.Method;
            if (string.Equals(method, "PROPFIND", StringComparison.OrdinalIgnoreCase))
            {
                var xml = new StringBuilder();
                xml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?><d:multistatus xmlns:d=\"DAV:\">");
                xml.Append("<d:response><d:href>/dav/PageArc/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>");
                foreach (var pair in _files.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                {
                    xml.Append("<d:response><d:href>/dav/PageArc/")
                        .Append(Uri.EscapeDataString(pair.Key))
                        .Append("</d:href><d:propstat><d:prop><d:displayname>")
                        .Append(pair.Key)
                        .Append("</d:displayname><d:getcontentlength>")
                        .Append(pair.Value.Payload.Length)
                        .Append("</d:getcontentlength><d:getlastmodified>")
                        .Append(pair.Value.Modified.ToString("R"))
                        .Append("</d:getlastmodified></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>");
                }
                xml.Append("</d:multistatus>");
                return new HttpResponseMessage((HttpStatusCode)207)
                {
                    Content = new StringContent(xml.ToString(), Encoding.UTF8, "application/xml")
                };
            }

            if (request.Method == HttpMethod.Options)
                return new HttpResponseMessage(HttpStatusCode.OK);

            var fileName = request.RequestUri is null
                ? string.Empty
                : Uri.UnescapeDataString(Path.GetFileName(request.RequestUri.AbsolutePath));

            if (request.Method == HttpMethod.Put)
            {
                var payload = request.Content is null
                    ? []
                    : await request.Content.ReadAsByteArrayAsync(cancellationToken);
                _files[fileName] = (payload, DateTimeOffset.UtcNow);
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            if (request.Method == HttpMethod.Get)
            {
                if (!_files.TryGetValue(fileName, out var stored))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(stored.Payload)
                };
            }

            if (request.Method == HttpMethod.Head)
            {
                if (!_files.TryGetValue(fileName, out var stored))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([])
                };
                response.Content.Headers.ContentLength = stored.Payload.Length;
                response.Content.Headers.LastModified = stored.Modified;
                return response;
            }

            if (request.Method == HttpMethod.Delete)
            {
                _files.Remove(fileName);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }
    }
}
