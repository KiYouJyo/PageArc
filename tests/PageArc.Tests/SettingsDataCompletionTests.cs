using System.IO.Compression;
using System.Net;
using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class SettingsDataCompletionTests
{
    [Fact]
    public void SettingsDefaults_IncludeFigmaControls()
    {
        var settings = new AppSettings();
        Assert.Equal("windows", settings.AccentSource);
        Assert.Equal("medium", settings.PageWidth);
        Assert.Equal("recent", settings.LibrarySort);
        Assert.Equal("grid", settings.LibraryView);
        Assert.True(settings.ShowRecentBooks);
        Assert.True(settings.DuplicateDetection);
        Assert.Equal(string.Empty, settings.WebDavEndpoint);
        Assert.Equal(string.Empty, settings.WebDavUsername);
    }

    [Fact]
    public void WebDavSettings_RequireAnAbsoluteHttpEndpoint()
    {
        Assert.Equal("https", new WebDavConnectionSettings("https://example.com/dav/pagearc.json", "reader").GetEndpointUri().Scheme);
        var folder = new WebDavConnectionSettings("https://example.com/dav/PageArc/", "reader");
        Assert.Equal("https://example.com/dav/PageArc/PageArc-library.pagearcbackup", folder.GetEndpointUri().AbsoluteUri);
        Assert.Equal("https://example.com/dav/PageArc/", folder.GetCollectionUri().AbsoluteUri);
        Assert.Throws<ArgumentException>(() => new WebDavConnectionSettings("pagearc.json", "reader").GetEndpointUri());
        Assert.Throws<ArgumentException>(() => new WebDavConnectionSettings("file:///pagearc.json", "reader").GetEndpointUri());
    }

    [Fact]
    public void ReadingBackupMerge_PrefersNewerProgressAndAnnotations()
    {
        var older = new DateTimeOffset(2026, 8, 20, 1, 0, 0, TimeSpan.Zero);
        var newer = older.AddHours(2);
        var local = new PageArcReadingBackup
        {
            Annotations = [new ReaderAnnotation { Id = "note", BookId = "book", Note = "old", UpdatedAt = older }],
            Progress = [new BookReadingProgressBackup { BookId = "book", Progress = 0.25, LastOpenedAt = older }]
        };
        var remote = new PageArcReadingBackup
        {
            Annotations = [new ReaderAnnotation { Id = "note", BookId = "book", Note = "new", UpdatedAt = newer }],
            Progress = [new BookReadingProgressBackup { BookId = "book", Progress = 0.75, LastOpenedAt = newer }]
        };

        var merged = ReadingBackupService.Merge(local, remote);

        Assert.Equal("new", Assert.Single(merged.Annotations).Note);
        Assert.Equal(0.75, Assert.Single(merged.Progress).Progress, 6);
    }

    [Fact]
    public void LibraryView_RoundTripsThroughSettingsService()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-library-view-{Guid.NewGuid():N}");
        var file = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(root);
        try
        {
            var settings = new SettingsService(file); settings.Load(); settings.Update(value => value.LibraryView = "list");
            var reloaded = new SettingsService(file); reloaded.Load(); Assert.Equal("list", reloaded.Current.LibraryView);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ReadingBackup_RoundTripsBookmarksAnnotationsAndProgress()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-backup-{Guid.NewGuid():N}");
        var readingPath = Path.Combine(root, "reading-data.json");
        var backupPath = Path.Combine(root, "backup.json");
        Directory.CreateDirectory(root);
        try
        {
            var reading = new ReadingDataService(readingPath); reading.Load();
            reading.ToggleBookmark("book-1", new FlowContentLocator(2, 0.25), "Chapter", "Bookmark snippet");
            reading.SaveAnnotation(new ReaderAnnotation { BookId = "book-1", Locator = new FlowContentLocator(2, 0.4, TextQuote: "Highlighted quote"), ChapterTitle = "Chapter", Quote = "Highlighted quote", Note = "A note", HighlightColor = "blue" });
            var books = new[] { new BookEntry { Id = "book-1", FilePath = Path.Combine(root, "fixture.epub"), Format = "EPUB", Title = "Fixture", Author = "Author", FileFingerprint = "fingerprint", Progress = 0.65, SpineIndex = 2, SectionFraction = 0.4, LastOpenedAt = new DateTimeOffset(2026, 8, 17, 1, 2, 3, TimeSpan.Zero) } };
            var service = new ReadingBackupService();
            await service.ExportAsync(backupPath, reading, books);
            var backup = ReadingBackupService.Read(backupPath);
            Assert.Equal(2, backup.SchemaVersion);
            Assert.Single(backup.Bookmarks);
            Assert.Single(backup.Annotations);
            Assert.Equal("fingerprint", Assert.Single(backup.Books).FileFingerprint);
            var progress = Assert.Single(backup.Progress);
            Assert.Equal("book-1", progress.BookId);
            Assert.Equal(0.65, progress.Progress, 6);
            Assert.Equal(2, progress.SectionIndex);
            Assert.Equal(0.4, progress.SectionFraction, 6);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task FullBackupPackage_ContainsManifestAndBookPayload()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-full-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var bookPath = Path.Combine(root, "fixture.epub");
            var packagePath = Path.Combine(root, "backup.pagearcbackup");
            await File.WriteAllBytesAsync(bookPath, [1, 2, 3, 4, 5]);

            var reading = new ReadingDataService(Path.Combine(root, "reading.json"));
            reading.Load();
            reading.ToggleBookmark("book-1", new FlowContentLocator(0, 0.2), "Chapter", "Bookmark");

            var book = new BookEntry
            {
                Id = "book-1",
                FilePath = bookPath,
                Format = "EPUB",
                Title = "Fixture",
                FileFingerprint = "hash"
            };

            var service = new ReadingBackupService();
            await service.ExportPackageAsync(packagePath, reading, [book]);

            var manifest = ReadingBackupService.ReadPackage(packagePath);
            Assert.Single(manifest.Books);
            Assert.Single(manifest.Bookmarks);

            using var source = File.OpenRead(packagePath);
            using var archive = new ZipArchive(source, ZipArchiveMode.Read);
            Assert.NotNull(archive.GetEntry(ReadingBackupService.PackageManifestEntryName));
            var bookEntry = Assert.Single(archive.Entries, entry => entry.FullName.StartsWith("books/book-1/", StringComparison.Ordinal));
            Assert.EndsWith("fixture.epub", bookEntry.FullName, StringComparison.Ordinal);
            Assert.Equal(5, bookEntry.Length);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task WebDavSyncService_TransfersArchiveAgainstFolderEndpoint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-webdav-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var handler = new InMemoryWebDavHandler();
            using var client = new HttpClient(handler);
            var service = new WebDavSyncService(client);
            var settings = new WebDavConnectionSettings("https://example.com/dav/PageArc/", "reader");
            await service.TestConnectionAsync(settings, "secret");

            var sourcePath = Path.Combine(root, "source.pagearcbackup");
            var destinationPath = Path.Combine(root, "download.pagearcbackup");
            await File.WriteAllBytesAsync(sourcePath, [9, 8, 7, 6]);

            await service.UploadFileAsync(settings, "secret", sourcePath);
            Assert.Equal(settings.GetEndpointUri(), handler.LastPutUri);
            Assert.True(await service.DownloadFileAsync(settings, "secret", destinationPath));
            Assert.Equal(new byte[] { 9, 8, 7, 6 }, await File.ReadAllBytesAsync(destinationPath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CacheClear_RemovesGeneratedFilesButKeepsExternalDataAndResetsCachedCoverPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-cache-{Guid.NewGuid():N}");
        var cache = Path.Combine(root, "Cache");
        var data = Path.Combine(root, "settings.json");
        var cachedCover = Path.Combine(cache, "Covers", "book.jpg");
        var externalCover = Path.Combine(root, "external-cover.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(cachedCover)!);
        File.WriteAllText(cachedCover, "cache"); File.WriteAllText(externalCover, "external"); File.WriteAllText(data, "user-data");
        try
        {
            var cachedBook = new BookEntry { CoverPath = cachedCover }; var externalBook = new BookEntry { CoverPath = externalCover };
            var changed = CacheMaintenanceService.ClearGeneratedCache(cache, new[] { cachedBook, externalBook });
            Assert.Equal(1, changed); Assert.Null(cachedBook.CoverPath); Assert.Equal(externalCover, externalBook.CoverPath);
            Assert.True(File.Exists(data)); Assert.Equal("user-data", File.ReadAllText(data)); Assert.True(Directory.Exists(cache)); Assert.False(File.Exists(cachedCover));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Categories_PersistNewCategoryAndRecoverBookAssignedCategory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-category-{Guid.NewGuid():N}");
        var file = Path.Combine(root, "categories.json"); Directory.CreateDirectory(root);
        try
        {
            var categories = new CategoryService(file); categories.Load([]); categories.Add("Research");
            var reloaded = new CategoryService(file); reloaded.Load(new[] { new BookEntry { Collection = "Design" } });
            Assert.Contains(reloaded.Categories, x => x.Name == "Research"); Assert.Contains(reloaded.Categories, x => x.Name == "Design");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class InMemoryWebDavHandler : HttpMessageHandler
    {
        private byte[]? _payload;

        public Uri? LastPutUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (string.Equals(request.Method.Method, "PROPFIND", StringComparison.OrdinalIgnoreCase))
                return new HttpResponseMessage((HttpStatusCode)207);

            if (request.Method == HttpMethod.Put)
            {
                LastPutUri = request.RequestUri;
                _payload = request.Content is null
                    ? []
                    : await request.Content.ReadAsByteArrayAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            if (request.Method == HttpMethod.Get)
            {
                if (_payload is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(_payload)
                };
            }

            if (request.Method == HttpMethod.Options)
                return new HttpResponseMessage(HttpStatusCode.OK);

            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }
    }
}
