using System.IO.Compression;
using PageArc.Models;
using PageArc.Services;
using PageArc.Services.Conversion;
using Xunit;

namespace PageArc.Tests;

public sealed class NormalizedFlowAdapterTests
{
    [Theory]
    [InlineData("MOBI", ".mobi")]
    [InlineData("AZW3", ".azw3")]
    [InlineData("LIT", ".lit")]
    public async Task Adapter_NormalizesLegacyFormatsIntoTheUnifiedFlowContract(string format, string extension)
    {
        var bookId = $"normalized-{Guid.NewGuid():N}";
        var input = Path.Combine(Path.GetTempPath(), $"pagearc-{Guid.NewGuid():N}{extension}");
        await File.WriteAllBytesAsync(input, [1, 2, 3, 4]);

        try
        {
            var service = new EbookConversionService([new SyntheticEpubProvider()]);
            var adapter = new CalibreNormalizedFlowAdapter(service);
            var book = new BookEntry
            {
                Id = bookId,
                FilePath = input,
                Format = format,
                Title = "Legacy fixture"
            };

            await using var source = await adapter.OpenAsync(book);
            Assert.Equal(format, source.Document.Format);
            Assert.Equal("Normalized fixture", source.Document.Title);
            Assert.Single(source.Document.Sections);
            Assert.Single(source.Document.Toc);

            var section = await source.LoadSectionAsync(0);
            Assert.Contains("Normalized flow content.", section.PlainText, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            var normalized = Path.Combine(AppPaths.NormalizedBooksRoot, bookId);
            if (Directory.Exists(normalized)) Directory.Delete(normalized, true);
            var extraction = Path.Combine(AppPaths.BooksCacheRoot, $"{bookId}-normalized");
            if (Directory.Exists(extraction)) Directory.Delete(extraction, true);
        }
    }

    private sealed class SyntheticEpubProvider : IEbookConversionProvider
    {
        public string Id => "synthetic-test";
        public bool IsAvailable => true;

        public bool CanConvert(string inputFormat, string outputFormat) =>
            new[] { "MOBI", "AZW3", "LIT" }.Contains(BookFormatRegistry.Normalize(inputFormat), StringComparer.OrdinalIgnoreCase)
            && string.Equals(BookFormatRegistry.Normalize(outputFormat), "EPUB", StringComparison.OrdinalIgnoreCase);

        public Task<EbookConversionResult> ConvertAsync(EbookConversionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = request.OutputPath ?? throw new InvalidOperationException("Test output path is required.");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            if (File.Exists(output)) File.Delete(output);

            using var archive = ZipFile.Open(output, ZipArchiveMode.Create);
            Write(archive, "mimetype", "application/epub+zip");
            Write(archive, "META-INF/container.xml", """
                <?xml version="1.0"?>
                <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
                  <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
                </container>
                """);
            Write(archive, "OEBPS/content.opf", """
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:title>Normalized fixture</dc:title><dc:creator>PageArc Test</dc:creator>
                  </metadata>
                  <manifest>
                    <item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/>
                    <item id="chapter" href="chapter.xhtml" media-type="application/xhtml+xml"/>
                  </manifest>
                  <spine><itemref idref="chapter"/></spine>
                </package>
                """);
            Write(archive, "OEBPS/nav.xhtml", """
                <html xmlns="http://www.w3.org/1999/xhtml"><body><nav><ol><li><a href="chapter.xhtml">Chapter</a></li></ol></nav></body></html>
                """);
            Write(archive, "OEBPS/chapter.xhtml", """
                <html xmlns="http://www.w3.org/1999/xhtml"><body><h1>Chapter</h1><p>Normalized flow content.</p></body></html>
                """);
            return Task.FromResult(EbookConversionResult.Completed(output));
        }

        private static void Write(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
    }
}
