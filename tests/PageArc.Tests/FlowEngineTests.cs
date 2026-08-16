using PageArc.Models;
using PageArc.Services;
using PageArc.Services.Conversion;
using Xunit;

namespace PageArc.Tests;

public sealed class FlowEngineTests
{
    [Fact]
    public void BookFormatRegistry_ContainsTheFivePageArcFormats()
    {
        Assert.Equal(["EPUB", "FB2", "MOBI", "AZW3", "LIT"], BookFormatRegistry.RequiredFormats.Select(x => x.Id).ToArray());
        Assert.Equal("MOBI", BookFormatRegistry.Normalize("AZW"));
        Assert.Equal("AZW3", BookFormatRegistry.Normalize("KF8"));
        Assert.Equal("MOBI", BookFormatRegistry.FormatFromPath("book.azw"));
        Assert.True(BookFormatRegistry.IsSupportedPath("book.lit"));
    }

    [Fact]
    public void CalibreProvider_DeclaresCompleteCrossFormatMatrix()
    {
        var provider = new CalibreConversionProvider(Path.Combine(Path.GetTempPath(), "pagearc-missing-ebook-convert.exe"));
        var formats = BookFormatRegistry.RequiredFormats.Select(x => x.Id).ToArray();
        foreach (var input in formats)
        foreach (var output in formats)
        {
            Assert.Equal(!string.Equals(input, output, StringComparison.OrdinalIgnoreCase), provider.CanConvert(input, output));
        }
    }

    [Fact]
    public void ConversionOutputPath_NeverOverwritesTheSource()
    {
        var input = Path.Combine(Path.GetTempPath(), $"pagearc-{Guid.NewGuid():N}.epub");
        File.WriteAllText(input, "fixture");
        try
        {
            var epub = EbookConversionService.CreateOutputPath(input, "EPUB");
            var fb2 = EbookConversionService.CreateOutputPath(input, "FB2");
            Assert.NotEqual(input, epub, StringComparer.OrdinalIgnoreCase);
            Assert.EndsWith(".fb2", fb2, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(input);
        }
    }

    [Fact]
    public async Task Fb2Adapter_ExposesReflowSectionsMetadataAndToc()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pagearc-{Guid.NewGuid():N}.fb2");
        await File.WriteAllTextAsync(path, """
            <?xml version="1.0" encoding="utf-8"?>
            <FictionBook xmlns="http://www.gribuser.ru/xml/fictionbook/2.0">
              <description>
                <title-info>
                  <book-title>PageArc FB2 Test</book-title>
                  <author><first-name>Test</first-name><last-name>Author</last-name></author>
                  <lang>en</lang>
                </title-info>
              </description>
              <body>
                <section id="one"><title><p>Chapter One</p></title><p>Hello <emphasis>PageArc</emphasis>.</p></section>
                <section id="two"><title><p>Chapter Two</p></title><p>Second section.</p></section>
              </body>
            </FictionBook>
            """);

        try
        {
            var book = new BookEntry { FilePath = path, Format = "FB2", Title = "Fallback" };
            var engine = new FlowReaderEngine();
            Assert.True(engine.CanOpen(book));
            await using var source = await engine.OpenAsync(book);

            Assert.Equal("FB2", source.Document.Format);
            Assert.Equal("PageArc FB2 Test", source.Document.Title);
            Assert.Equal("Test Author", source.Document.Author);
            Assert.Equal("en", source.Document.Language);
            Assert.Equal(2, source.Document.Sections.Count);
            Assert.Equal(2, source.Document.Toc.Count);
            Assert.Equal(0, source.Document.Toc[0].SectionIndex);

            var section = await source.LoadSectionAsync(0);
            Assert.Contains("<h2>Chapter One</h2>", section.Html, StringComparison.Ordinal);
            Assert.Contains("<em>PageArc</em>", section.Html, StringComparison.Ordinal);
            Assert.Contains("Hello PageArc.", section.PlainText, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
