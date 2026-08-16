using System.Text.Json;
using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class LibraryScaleTests
{
    [Fact]
    public void LoadAndSave_PreservesTwoThousandLegacyEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-scale-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "library.json");
            var legacy = Enumerable.Range(0, 2000)
                .Select(index => new BookEntry
                {
                    Id = $"book-{index}",
                    FilePath = Path.Combine(root, $"missing-{index}.epub"),
                    Format = "EPUB",
                    Title = $"Book {index:0000}",
                    Progress = (index % 101) / 100d
                })
                .ToArray();
            File.WriteAllText(path, JsonSerializer.Serialize(legacy));

            var service = new LibraryService(path);
            service.Load();
            Assert.Equal(2000, service.Books.Count);
            Assert.All(service.Books, book => Assert.True(book.IsMissing));

            service.Save();
            var reloaded = new LibraryService(path);
            reloaded.Load();
            Assert.Equal(2000, reloaded.Books.Count);
            Assert.Equal("book-0", reloaded.Books[0].Id);
            Assert.Equal("book-1999", reloaded.Books[^1].Id);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
