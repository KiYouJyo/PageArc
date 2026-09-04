using PageArc.Models;
using PageArc.Services;
using PageArc.Services.Conversion;
using Xunit;

namespace PageArc.Tests;

public sealed class V095FeatureTests
{
    [Fact]
    public void BackupV2_restores_reading_data_by_content_fingerprint()
    {
        var root = NewTempDirectory();
        var sourceData = new ReadingDataService(Path.Combine(root, "source-reading.json"));
        var sourceBook = new BookEntry
        {
            Id = "old-book-id", FilePath = Path.Combine(root, "old.epub"), Format = "EPUB", Title = "The Same Book", Author = "Author",
            FileFingerprint = "ABC123", Progress = .62, SpineIndex = 3, SectionFraction = .41
        };
        sourceData.ToggleBookmark(sourceBook.Id, new FlowContentLocator(2, .3), "Chapter 3", "bookmark");
        sourceData.SaveAnnotation(new ReaderAnnotation { Id = "note-1", BookId = sourceBook.Id, Locator = new FlowContentLocator(3, .41), ChapterTitle = "Chapter 4", Quote = "quote", Note = "note" });

        var backupService = new ReadingBackupService();
        var backup = backupService.CreateBackup(sourceData, [sourceBook]);
        Assert.Equal(2, backup.SchemaVersion);
        Assert.Equal("ABC123", Assert.Single(backup.Books).FileFingerprint);

        var targetData = new ReadingDataService(Path.Combine(root, "target-reading.json"));
        var targetBook = new BookEntry { Id = "new-book-id", FilePath = Path.Combine(root, "new.epub"), Format = "EPUB", Title = "The Same Book", Author = "Author", FileFingerprint = "ABC123" };
        var result = backupService.Restore(backup, targetData, new List<BookEntry> { targetBook }, ReadingBackupRestoreMode.Merge);

        Assert.Equal(1, result.MatchedBooks);
        Assert.Equal(0, result.UnmatchedBooks);
        Assert.Single(targetData.GetBookmarks(targetBook.Id));
        Assert.Single(targetData.GetAnnotations(targetBook.Id));
        Assert.Equal(.62, targetBook.Progress, 3);
        Assert.Equal(3, targetBook.SpineIndex);
        Assert.Equal(.41, targetBook.SectionFraction, 3);
    }

    [Fact]
    public void Backup_replace_mode_discards_local_annotations()
    {
        var root = NewTempDirectory();
        var data = new ReadingDataService(Path.Combine(root, "reading.json"));
        data.SaveAnnotation(new ReaderAnnotation { Id = "local", BookId = "book", Quote = "local" });
        var backup = new PageArcReadingBackup
        {
            SchemaVersion = 2,
            Books = [new ReadingBackupBookIdentity { BookId = "book", Title = "Book", Format = "EPUB" }],
            Annotations = [new ReaderAnnotation { Id = "imported", BookId = "book", Quote = "imported" }]
        };
        var books = new List<BookEntry> { new() { Id = "book", Title = "Book", Format = "EPUB" } };
        new ReadingBackupService().Restore(backup, data, books, ReadingBackupRestoreMode.Replace);
        var annotations = data.GetAnnotations("book");
        Assert.Single(annotations);
        Assert.Equal("imported", annotations[0].Id);
    }

    [Fact]
    public void Shell_session_store_round_trips_and_rejects_duplicate_reader_tabs()
    {
        var root = NewTempDirectory();
        var path = Path.Combine(root, "shell-session.json");
        var manager = new ShellTabSessionManager();
        manager.ReplaceAll([
            new ShellTabSession("home", ShellTabKind.Home),
            new ShellTabSession("reader-a", ShellTabKind.Reader, "book-a"),
            new ShellTabSession("reader-b", ShellTabKind.Reader, "book-a")
        ]);
        Assert.Equal(2, manager.Tabs.Count);
        var store = new ShellSessionStore(path);
        store.Save(new ShellSessionState { SelectedTabId = "reader-a", Tabs = manager.Tabs.ToList() });
        var restored = store.Load();
        Assert.Equal(2, restored.Tabs.Count);
        Assert.Equal("reader-a", restored.SelectedTabId);
    }

    [Fact]
    public void Managed_conversion_provider_exposes_complete_on_demand_matrix()
    {
        var provider = new PageArcManagedConversionProvider(new ConversionRuntimeManager(new HttpClient(new OfflineHandler())));
        var service = new EbookConversionService([provider]);
        Assert.True(provider.IsAvailable);
        Assert.StartsWith("pagearc-managed-calibre-", provider.Id);
        Assert.Equal(20, service.GetRequiredCapabilityMatrix().Count);
        Assert.True(service.HasCompleteRequiredMatrix());
    }

    [Fact]
    public void Reader_enhancement_script_contains_cjk_footnote_and_image_viewer_contracts()
    {
        var script = ReaderEnhancementScript.Build("ja-JP");
        Assert.Contains("line-break: strict", script, StringComparison.Ordinal);
        Assert.Contains("ruby-position", script, StringComparison.Ordinal);
        Assert.Contains("writingMode.startsWith('vertical')", script, StringComparison.Ordinal);
        Assert.Contains("noteref", script, StringComparison.Ordinal);
        Assert.Contains("pagearc-footnote-layer", script, StringComparison.Ordinal);
        Assert.Contains("pagearc-image-viewer", script, StringComparison.Ordinal);
        Assert.Contains("pagearc-image-save", script, StringComparison.Ordinal);
        Assert.Contains("\"fit\"", script, StringComparison.Ordinal);
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "PageArc.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
    }
}
