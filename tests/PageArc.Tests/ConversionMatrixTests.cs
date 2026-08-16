using PageArc.Models;
using PageArc.Services;
using PageArc.Services.Conversion;
using Xunit;

namespace PageArc.Tests;

public sealed class ConversionMatrixTests
{
    [Fact]
    public void RequiredMatrix_ContainsEveryOrderedCrossFormatPair()
    {
        var service = new EbookConversionService([new CompleteProvider()]);
        var matrix = service.GetRequiredCapabilityMatrix();

        Assert.Equal(20, matrix.Count);
        Assert.All(matrix, capability => Assert.True(capability.IsAvailable));
        Assert.True(service.HasCompleteRequiredMatrix());
        Assert.Equal(4, matrix.Count(item => item.InputFormat == "LIT"));
        Assert.Equal(4, matrix.Count(item => item.OutputFormat == "LIT"));
        Assert.DoesNotContain(matrix, item => item.InputFormat == item.OutputFormat);
        Assert.All(matrix, item => Assert.Equal("complete-test", item.ProviderId));
    }

    [Fact]
    public void RequiredMatrix_ReportsUnavailablePairsInsteadOfPretendingSupport()
    {
        var service = new EbookConversionService([new EpubOnlyProvider()]);
        var matrix = service.GetRequiredCapabilityMatrix();

        Assert.Equal(20, matrix.Count);
        Assert.False(service.HasCompleteRequiredMatrix());
        Assert.True(matrix.Single(item => item.InputFormat == "FB2" && item.OutputFormat == "EPUB").IsAvailable);
        Assert.False(matrix.Single(item => item.InputFormat == "LIT" && item.OutputFormat == "AZW3").IsAvailable);
    }

    private sealed class CompleteProvider : IEbookConversionProvider
    {
        public string Id => "complete-test";
        public bool IsAvailable => true;
        public bool CanConvert(string inputFormat, string outputFormat) =>
            BookFormatRegistry.IsRequired(inputFormat)
            && BookFormatRegistry.IsRequired(outputFormat)
            && !string.Equals(BookFormatRegistry.Normalize(inputFormat), BookFormatRegistry.Normalize(outputFormat), StringComparison.OrdinalIgnoreCase);
        public Task<EbookConversionResult> ConvertAsync(EbookConversionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(EbookConversionResult.Failed("not used"));
    }

    private sealed class EpubOnlyProvider : IEbookConversionProvider
    {
        public string Id => "epub-only";
        public bool IsAvailable => true;
        public bool CanConvert(string inputFormat, string outputFormat) =>
            !string.Equals(BookFormatRegistry.Normalize(inputFormat), "EPUB", StringComparison.OrdinalIgnoreCase)
            && string.Equals(BookFormatRegistry.Normalize(outputFormat), "EPUB", StringComparison.OrdinalIgnoreCase);
        public Task<EbookConversionResult> ConvertAsync(EbookConversionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(EbookConversionResult.Failed("not used"));
    }
}
