using PageArc.Models;
using PageArc.Services;
using PageArc.Services.Conversion;
using Xunit;

namespace PageArc.Tests;

public sealed class SurfaceIntegrationTests
{
    [Fact]
    public void FigmaSurfaceInventory_IsCanonicalUniqueAndFunctionallyComplete()
    {
        var surfaces = PageArcFigmaSurfaces.Canonical;

        Assert.Equal(19, surfaces.Count);
        Assert.Equal(19, surfaces.Select(x => x.NodeId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(surfaces, surface => Assert.True(surface.IsFunctionallyComplete, surface.Name));
        Assert.Contains(surfaces, x => x.NodeId == "16:1750" && x.Name == "Settings");
        Assert.Contains(surfaces, x => x.NodeId == "16:1878" && x.Name == "About");
        Assert.Contains(surfaces, x => x.NodeId == "25:2" && x.Name == "Format conversion");
    }

    [Fact]
    public void PlannedFunctionalContracts_CoverActivationConversionAndBackup()
    {
        var deepLink = AppActivationRequestParser.CreateBookUri("book 42");
        var activation = AppActivationRequestParser.FromProtocol(deepLink);
        Assert.Equal(AppActivationRequestKind.Book, activation.Kind);
        Assert.Equal("book 42", activation.BookId);

        var conversion = new EbookConversionService([new CompleteProvider()]);
        var matrix = conversion.GetRequiredCapabilityMatrix();
        Assert.Equal(20, matrix.Count);
        Assert.All(matrix, item => Assert.True(item.IsAvailable));

        var root = Path.Combine(Path.GetTempPath(), $"pagearc-v09-contract-{Guid.NewGuid():N}");
        var readingPath = Path.Combine(root, "reading-data.json");
        Directory.CreateDirectory(root);
        try
        {
            var reading = new ReadingDataService(readingPath);
            reading.Load();
            reading.ToggleBookmark("book-42", new FlowContentLocator(1, 0.2), "Chapter", "Bookmark");
            reading.SaveAnnotation(new ReaderAnnotation
            {
                BookId = "book-42",
                Locator = new FlowContentLocator(1, 0.3, TextQuote: "Quote"),
                ChapterTitle = "Chapter",
                Quote = "Quote",
                Note = "Note"
            });

            var backup = new ReadingBackupService().CreateBackup(reading,
            [
                new BookEntry
                {
                    Id = "book-42",
                    Title = "Contract fixture",
                    Progress = 0.4,
                    SpineIndex = 1,
                    SectionFraction = 0.3
                }
            ]);

            Assert.Equal(2, backup.SchemaVersion);
            Assert.Single(backup.Bookmarks);
            Assert.Single(backup.Annotations);
            Assert.Single(backup.Progress);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class CompleteProvider : IEbookConversionProvider
    {
        public string Id => "v09-contract";
        public bool IsAvailable => true;
        public bool CanConvert(string inputFormat, string outputFormat) =>
            BookFormatRegistry.IsRequired(inputFormat)
            && BookFormatRegistry.IsRequired(outputFormat)
            && !string.Equals(BookFormatRegistry.Normalize(inputFormat), BookFormatRegistry.Normalize(outputFormat), StringComparison.OrdinalIgnoreCase);

        public Task<EbookConversionResult> ConvertAsync(EbookConversionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(EbookConversionResult.Failed("not used"));
    }
}
