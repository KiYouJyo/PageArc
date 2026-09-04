using PageArc.Models;

namespace PageArc.Services.Conversion;

public sealed class PageArcManagedConversionProvider : IEbookConversionProvider
{
    private readonly ConversionRuntimeManager _runtimeManager;

    public PageArcManagedConversionProvider(ConversionRuntimeManager? runtimeManager = null)
    {
        _runtimeManager = runtimeManager ?? new ConversionRuntimeManager();
    }

    public string Id => $"pagearc-managed-calibre-{ConversionRuntimeManager.PackageVersion}";

    // Availability here means PageArc can satisfy the capability on demand.
    // The heavy runtime itself may not be installed yet.
    public bool IsAvailable => _runtimeManager.IsSupported;

    public bool CanConvert(string inputFormat, string outputFormat)
    {
        var input = BookFormatRegistry.Normalize(inputFormat);
        var output = BookFormatRegistry.Normalize(outputFormat);
        return BookFormatRegistry.IsRequired(input)
               && BookFormatRegistry.IsRequired(output)
               && !string.Equals(input, output, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<EbookConversionResult> ConvertAsync(
        EbookConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return EbookConversionResult.Failed("The PageArc conversion runtime is not supported on this device.");

        try
        {
            var executable = await _runtimeManager.EnsureInstalledAsync(cancellationToken: cancellationToken);
            return await new CalibreConversionProvider(executable).ConvertAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Managed conversion runtime failed", ex);
            return EbookConversionResult.Failed($"PageArc could not prepare its conversion runtime: {ex.Message}");
        }
    }
}
