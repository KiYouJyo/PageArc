namespace PageArc.Models;

public sealed record EbookConversionOptions(
    bool KeepMetadata = true,
    bool KeepCover = true,
    bool KeepTableOfContents = true);

public sealed record EbookConversionRequest(
    string InputPath,
    string OutputFormat,
    string? OutputPath = null,
    EbookConversionOptions? Options = null);

public sealed record EbookConversionResult(
    bool Success,
    string? OutputPath = null,
    string? ErrorMessage = null,
    bool IsDrmProtected = false)
{
    public static EbookConversionResult Completed(string outputPath) => new(true, outputPath);
    public static EbookConversionResult Failed(string errorMessage, bool isDrmProtected = false) =>
        new(false, null, errorMessage, isDrmProtected);
}
