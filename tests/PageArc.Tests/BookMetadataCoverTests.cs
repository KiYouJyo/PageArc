using System.IO.Compression;
using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class BookMetadataCoverTests
{
    [Fact]
    public async Task Epub2GuideCover_ExtractsRasterImageReferencedByTitlePage()
    {
        var bookId = $"cover-{Guid.NewGuid():N}";
        var epubPath = Path.Combine(Path.GetTempPath(), $"pagearc-cover-{Guid.NewGuid():N}.epub");
        var expectedCover = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 0xFF, 0xD9 };

        try
        {
            using (var archive = ZipFile.Open(epubPath, ZipArchiveMode.Create))
            {
                WriteText(archive, "mimetype", "application/epub+zip");
                WriteText(archive, "META-INF/container.xml", """
                    <?xml version="1.0"?>
                    <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
                      <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
                    </container>
                    """);
                WriteText(archive, "OEBPS/content.opf", """
                    <?xml version="1.0" encoding="utf-8"?>
                    <package xmlns="http://www.idpf.org/2007/opf" version="2.0">
                      <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                        <dc:title>Guide Cover Fixture</dc:title>
                      </metadata>
                      <manifest>
                        <item id="titlepage" href="titlepage.xhtml" media-type="application/xhtml+xml"/>
                        <item id="cover-jpg" href="images/cover.jpg" media-type="image/jpeg"/>
                        <item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml"/>
                      </manifest>
                      <spine><itemref idref="chapter"/></spine>
                      <guide><reference type="cover" title="Cover" href="titlepage.xhtml"/></guide>
                    </package>
                    """);
                WriteText(archive, "OEBPS/titlepage.xhtml", """
                    <html xmlns="http://www.w3.org/1999/xhtml"><body>
                      <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink">
                        <image xlink:href="images/cover.jpg"/>
                      </svg>
                    </body></html>
                    """);
                WriteText(archive, "OEBPS/chapter.xhtml", "<html><body>Chapter</body></html>");
                var image = archive.CreateEntry("OEBPS/images/cover.jpg");
                await using var stream = image.Open();
                await stream.WriteAsync(expectedCover);
            }

            var book = new BookEntry { Id = bookId, FilePath = epubPath, Format = "EPUB", Title = "Fixture" };
            var metadata = await BookMetadataService.ReadAsync(book);

            Assert.False(string.IsNullOrWhiteSpace(metadata.CoverPath));
            Assert.True(File.Exists(metadata.CoverPath));
            Assert.Equal(expectedCover, await File.ReadAllBytesAsync(metadata.CoverPath!));
        }
        finally
        {
            if (File.Exists(epubPath)) File.Delete(epubPath);
            if (Directory.Exists(AppPaths.CoversRoot))
            {
                foreach (var path in Directory.EnumerateFiles(AppPaths.CoversRoot, bookId + ".*"))
                    File.Delete(path);
            }
        }
    }

    private static void WriteText(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}
