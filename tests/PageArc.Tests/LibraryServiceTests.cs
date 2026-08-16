using System.Text.Json;
using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class LibraryServiceTests
{
    [Fact]
    public void Load_PreservesMissingBookRecords()
    {
        var root = CreateTempDirectory();
        try
        {
            var libraryFile = Path.Combine(root, "library.json");
            var missingPath = Path.Combine(root, "missing.epub");
            File.WriteAllText(libraryFile, JsonSerializer.Serialize(new[]
            {
                new BookEntry { Id = "missing", FilePath = missingPath, Format = "EPUB", Title = "Missing book", FileSize = 42 }
            }));

            var library = new LibraryService(libraryFile);
            library.Load();

            var book = Assert.Single(library.Books);
            Assert.Equal("missing", book.Id);
            Assert.True(book.IsMissing);
            Assert.Equal("EPUB", book.Format);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ImportDetailed_DetectsDuplicateContentAcrossDifferentPaths()
    {
        var root = CreateTempDirectory();
        try
        {
            var first = Path.Combine(root, "first.mobi");
            var second = Path.Combine(root, "second.mobi");
            var bytes = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();
            await File.WriteAllBytesAsync(first, bytes);
            await File.WriteAllBytesAsync(second, bytes);

            var library = new LibraryService(Path.Combine(root, "library.json"));
            var added = await library.ImportDetailedAsync(first);
            var duplicate = await library.ImportDetailedAsync(second);

            Assert.Equal(LibraryImportDisposition.Added, added.Disposition);
            Assert.Equal(LibraryImportDisposition.DuplicateContent, duplicate.Disposition);
            Assert.NotNull(added.Book);
            Assert.Same(added.Book, duplicate.Book);
            Assert.Single(library.Books);
            Assert.False(string.IsNullOrWhiteSpace(added.Book!.FileFingerprint));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ImportMany_ReturnsTruthfulAddedUnsupportedAndMissingCounts()
    {
        var root = CreateTempDirectory();
        try
        {
            var supported = Path.Combine(root, "book.azw3");
            var unsupported = Path.Combine(root, "notes.txt");
            var missing = Path.Combine(root, "gone.fb2");
            await File.WriteAllBytesAsync(supported, [1, 2, 3, 4, 5]);
            await File.WriteAllTextAsync(unsupported, "not an ebook");

            var library = new LibraryService(Path.Combine(root, "library.json"));
            var summary = await library.ImportManyAsync([supported, unsupported, missing]);

            Assert.Equal(3, summary.Total);
            Assert.Equal(1, summary.Added);
            Assert.Equal(1, summary.Unsupported);
            Assert.Equal(1, summary.Failed);
            Assert.Single(library.Books);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RefreshFileStates_MarksDeletedSourceWithoutDroppingBook()
    {
        var root = CreateTempDirectory();
        try
        {
            var source = Path.Combine(root, "book.mobi");
            await File.WriteAllBytesAsync(source, [7, 8, 9]);
            var library = new LibraryService(Path.Combine(root, "library.json"));
            var book = await library.ImportAsync(source);
            File.Delete(source);

            Assert.True(library.RefreshFileStates());
            Assert.True(book.IsMissing);
            Assert.Single(library.Books);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pagearc-library-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
