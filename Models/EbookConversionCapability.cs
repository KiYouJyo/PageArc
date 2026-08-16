namespace PageArc.Models;

public sealed record EbookConversionCapability(
    string InputFormat,
    string OutputFormat,
    bool IsAvailable,
    string? ProviderId = null)
{
    public string Pair => $"{InputFormat}→{OutputFormat}";
}
