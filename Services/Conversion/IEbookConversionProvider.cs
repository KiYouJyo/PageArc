using PageArc.Models;

namespace PageArc.Services.Conversion;

public interface IEbookConversionProvider
{
    string Id { get; }
    bool IsAvailable { get; }
    bool CanConvert(string inputFormat, string outputFormat);
    Task<EbookConversionResult> ConvertAsync(EbookConversionRequest request, CancellationToken cancellationToken = default);
}
