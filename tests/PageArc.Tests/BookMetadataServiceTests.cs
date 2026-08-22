using System.IO.Compression;
using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class BookMetadataServiceTests
{
    [Fact]
    public void UnsupportedNativeFormats_UseBundledCalibreCoverExtractionAndPersistedLibraryRepair()
    {
        var root = FindRepoRoot();
        var metadata = File.ReadAllText(Path.Combine(root, "Services", "BookMetadataService.cs"));
        var library = File.ReadAllText(Path.Combine(root, "Services", "LibraryService.cs"));
        var app = File.ReadAllText(Path.Combine(root, "App.xaml.cs"));
        var covers = File.ReadAllText(Path.Combine(root, "Pages", "LibraryPage.Covers.cs"));

        Assert.Contains("ebook-meta.exe", metadata, StringComparison.Ordinal);
        Assert.Contains("--get-cover", metadata, StringComparison.Ordinal);
        Assert.Contains("WriteCoverAsync", metadata, StringComparison.Ordinal);
        Assert.Contains("EnsureImportedCoversAsync", library, StringComparison.Ordinal);
        Assert.Contains("Library.EnsureImportedCoversAsync", app, StringComparison.Ordinal);
        Assert.DoesNotContain("BookMetadataService.ReadAsync", covers, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EpubMetadata_ExtractsRichFieldsAndCover()
    {
        var id = Guid.NewGuid().ToString("N");
        var path = Path.Combine(Path.GetTempPath(), $"pagearc-meta-{id}.epub");
        try
        {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                Write(archive, "META-INF/container.xml", """
                    <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
                      <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
                    </container>
                    """);
                Write(archive, "OEBPS/content.opf", """
                    <package xmlns="http://www.idpf.org/2007/opf" version="3.0">
                      <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                        <dc:title>Rich EPUB</dc:title><dc:creator>A. Writer</dc:creator><dc:language>en</dc:language>
                        <dc:publisher>PageArc Press</dc:publisher><dc:description>A useful description.</dc:description>
                      </metadata>
                      <manifest><item id="cover" href="cover.png" media-type="image/png" properties="cover-image"/></manifest>
                    </package>
                    """);
                var cover = archive.CreateEntry("OEBPS/cover.png");
                await using var stream = cover.Open();
                await stream.WriteAsync(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            }

            var book = new BookEntry { Id = id, FilePath = path, Format = "EPUB", Title = "fallback" };
            var metadata = await BookMetadataService.ReadAsync(book);

            Assert.Equal("Rich EPUB", metadata.Title);
            Assert.Equal("A. Writer", metadata.Author);
            Assert.Equal("en", metadata.Language);
            Assert.Equal("PageArc Press", metadata.Publisher);
            Assert.Equal("A useful description.", metadata.Description);
            Assert.NotNull(metadata.CoverPath);
            Assert.True(File.Exists(metadata.CoverPath));
            Assert.EndsWith(".png", metadata.CoverPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            DeleteCovers(id);
        }
    }

    [Fact]
    public async Task Fb2Metadata_ExtractsTitleAuthorLanguagePublisherAnnotationAndCover()
    {
        var id = Guid.NewGuid().ToString("N");
        var path = Path.Combine(Path.GetTempPath(), $"pagearc-meta-{id}.fb2");
        var encoded = Convert.ToBase64String(new byte[] { 255, 216, 255, 224, 0, 16 });
        await File.WriteAllTextAsync(path, $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <FictionBook xmlns="http://www.gribuser.ru/xml/fictionbook/2.0" xmlns:l="http://www.w3.org/1999/xlink">
              <description>
                <title-info>
                  <genre>prose</genre><author><first-name>K.</first-name><last-name>Ito</last-name></author>
                  <book-title>Rich FB2</book-title><annotation><p>  A   compact annotation. </p></annotation>
                  <coverpage><image l:href="#cover"/></coverpage><lang>ja</lang>
                </title-info>
                <publish-info><publisher>Tokyo Books</publisher></publish-info>
              </description>
              <body><section><p>Hello</p></section></body>
              <binary id="cover" content-type="image/jpeg">{{encoded}}</binary>
            </FictionBook>
            """);

        try
        {
            var book = new BookEntry { Id = id, FilePath = path, Format = "FB2", Title = "fallback" };
            var metadata = await BookMetadataService.ReadAsync(book);

            Assert.Equal("Rich FB2", metadata.Title);
            Assert.Equal("K. Ito", metadata.Author);
            Assert.Equal("ja", metadata.Language);
            Assert.Equal("Tokyo Books", metadata.Publisher);
            Assert.Equal("A compact annotation.", metadata.Description);
            Assert.NotNull(metadata.CoverPath);
            Assert.True(File.Exists(metadata.CoverPath));
            Assert.EndsWith(".jpg", metadata.CoverPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            DeleteCovers(id);
        }
    }

    private static void Write(ZipArchive archive, string path, string text)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(text);
    }

    private static void DeleteCovers(string id)
    {
        if (!Directory.Exists(AppPaths.CoversRoot)) return;
        foreach (var path in Directory.EnumerateFiles(AppPaths.CoversRoot, id + ".*"))
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PageArc.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
