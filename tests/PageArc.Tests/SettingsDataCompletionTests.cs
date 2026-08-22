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
}
