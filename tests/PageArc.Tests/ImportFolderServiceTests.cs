using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class ImportFolderServiceTests
{
    [Fact]
    public async Task AddAsync_ScansSupportedBooksRecursivelyAndPersistsFolder()
    {
        var root = CreateTempDirectory();
        try
        {
            var watched = Path.Combine(root, "watched");
            var nested = Path.Combine(watched, "nested");
            Directory.CreateDirectory(nested);
            await File.WriteAllBytesAsync(Path.Combine(watched, "a.mobi"), [1, 2, 3]);
            await File.WriteAllBytesAsync(Path.Combine(nested, "b.azw3"), [4, 5, 6]);
            await File.WriteAllTextAsync(Path.Combine(nested, "ignore.txt"), "x");

            var library = new LibraryService(Path.Combine(root, "library.json"));
            var state = Path.Combine(root, "folders.json");
            var service = new ImportFolderService(library, state);
            var result = await service.AddAsync(watched);

            Assert.Equal(2, result.SupportedFilesFound);
            Assert.Equal(2, result.ImportSummary.Added);
            Assert.Equal(2, library.Books.Count);
            var folder = Assert.Single(service.Folders);
            Assert.Equal(2, folder.BookCount);
            Assert.NotNull(folder.LastScannedAt);
            Assert.True(File.Exists(state));

            var reloaded = new ImportFolderService(library, state);
            reloaded.Load();
            var persisted = Assert.Single(reloaded.Folders);
            Assert.Equal(folder.Id, persisted.Id);
            Assert.Equal(Path.GetFullPath(watched), persisted.FolderPath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RescanAsync_UsesLibraryDuplicateRulesInsteadOfAddingCopies()
    {
        var root = CreateTempDirectory();
        try
        {
            var watched = Path.Combine(root, "watched");
            Directory.CreateDirectory(watched);
            var book = Path.Combine(watched, "a.mobi");
            await File.WriteAllBytesAsync(book, [8, 9, 10]);

            var library = new LibraryService(Path.Combine(root, "library.json"));
            var service = new ImportFolderService(library, Path.Combine(root, "folders.json"));
            var first = await service.AddAsync(watched);
            var second = await service.RescanAsync(Assert.Single(service.Folders));

            Assert.Equal(1, first.ImportSummary.Added);
            Assert.Equal(1, second.ImportSummary.Existing);
            Assert.Single(library.Books);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task MissingFolder_RemainsTrackedAsUnavailable()
    {
        var root = CreateTempDirectory();
        try
        {
            var watched = Path.Combine(root, "watched");
            Directory.CreateDirectory(watched);
            var library = new LibraryService(Path.Combine(root, "library.json"));
            var service = new ImportFolderService(library, Path.Combine(root, "folders.json"));
            await service.AddAsync(watched);
            var folder = Assert.Single(service.Folders);
            Directory.Delete(watched, true);

            var result = await service.RescanAsync(folder);

            Assert.False(folder.IsAvailable);
            Assert.Equal(0, result.SupportedFilesFound);
            Assert.Single(service.Folders);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pagearc-folders-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
