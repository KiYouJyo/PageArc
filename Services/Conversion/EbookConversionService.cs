using PageArc.Models;

namespace PageArc.Services.Conversion;

public sealed class EbookConversionService
{
    private readonly IReadOnlyList<IEbookConversionProvider> _providers;

    public EbookConversionService(IEnumerable<IEbookConversionProvider>? providers = null)
    {
        _providers = (providers ?? [new CalibreConversionProvider()]).ToArray();
    }

    public IReadOnlyList<IEbookConversionProvider> Providers => _providers;

    public bool CanConvert(string inputFormat, string outputFormat) =>
        _providers.Any(provider => provider.IsAvailable && provider.CanConvert(inputFormat, outputFormat));

    public async Task<EbookConversionResult> ConvertAsync(EbookConversionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(request.InputPath))
            return EbookConversionResult.Failed("The source ebook does not exist.");

        var inputFormat = BookFormatRegistry.FormatFromPath(request.InputPath);
        if (string.IsNullOrWhiteSpace(inputFormat))
            return EbookConversionResult.Failed("The source ebook format is not supported by PageArc.");

        var outputFormat = BookFormatRegistry.Normalize(request.OutputFormat);
        if (!BookFormatRegistry.IsRequired(outputFormat))
            return EbookConversionResult.Failed($"The output ebook format is not supported: {request.OutputFormat}.");
        if (string.Equals(inputFormat, outputFormat, StringComparison.OrdinalIgnoreCase))
            return EbookConversionResult.Failed("Source and output formats are the same.");

        var provider = _providers.FirstOrDefault(x => x.IsAvailable && x.CanConvert(inputFormat, outputFormat));
        if (provider is null)
            return EbookConversionResult.Failed($"No installed conversion provider can convert {inputFormat} to {outputFormat}.");

        var outputPath = string.IsNullOrWhiteSpace(request.OutputPath)
            ? CreateOutputPath(request.InputPath, outputFormat)
            : request.OutputPath!;
        var normalizedRequest = request with { OutputFormat = outputFormat, OutputPath = outputPath };
        return await provider.ConvertAsync(normalizedRequest, cancellationToken);
    }

    public static string CreateOutputPath(string inputPath, string outputFormat)
    {
        var descriptor = BookFormatRegistry.GetRequired(outputFormat);
        var directory = Path.GetDirectoryName(Path.GetFullPath(inputPath)) ?? Environment.CurrentDirectory;
        var stem = Path.GetFileNameWithoutExtension(inputPath);
        var candidate = Path.Combine(directory, stem + descriptor.PrimaryExtension);
        if (!File.Exists(candidate) && !string.Equals(candidate, inputPath, StringComparison.OrdinalIgnoreCase)) return candidate;

        for (var i = 1; ; i++)
        {
            candidate = Path.Combine(directory, $"{stem}.converted-{i}{descriptor.PrimaryExtension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
