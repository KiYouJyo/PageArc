using PageArc.Models;

namespace PageArc.Services.Conversion;

public sealed class PageArcBundledConversionProvider : IEbookConversionProvider
{
    public const string RuntimeVersion = "9.13.0";
    public const string RuntimeExecutableName = "ebook-convert.exe";
    private readonly string? _executablePath;

    public PageArcBundledConversionProvider(string? executablePath = null, string? appBaseDirectory = null)
    {
        _executablePath = IsExecutable(executablePath)
            ? Path.GetFullPath(executablePath!)
            : ResolveBundledExecutable(appBaseDirectory);
    }

    public string Id => $"pagearc-bundled-calibre-{RuntimeVersion}";
    public bool IsAvailable => IsExecutable(_executablePath);

    public bool CanConvert(string inputFormat, string outputFormat)
    {
        var input = BookFormatRegistry.Normalize(inputFormat);
        var output = BookFormatRegistry.Normalize(outputFormat);
        return BookFormatRegistry.IsRequired(input)
               && BookFormatRegistry.IsRequired(output)
               && !string.Equals(input, output, StringComparison.OrdinalIgnoreCase);
    }

    public Task<EbookConversionResult> ConvertAsync(EbookConversionRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return Task.FromResult(EbookConversionResult.Failed("The PageArc bundled conversion runtime is missing or incomplete."));

        var provider = new CalibreConversionProvider(_executablePath);
        return provider.ConvertAsync(request, cancellationToken);
    }

    public static string? ResolveBundledExecutable(string? appBaseDirectory = null)
    {
        var root = string.IsNullOrWhiteSpace(appBaseDirectory) ? AppContext.BaseDirectory : Path.GetFullPath(appBaseDirectory);
        var candidates = new[]
        {
            Path.Combine(root, "ThirdParty", "calibre", "runtime", RuntimeExecutableName),
            Path.Combine(root, "calibre-runtime", RuntimeExecutableName),
            Path.Combine(root, RuntimeExecutableName)
        };
        return candidates.FirstOrDefault(IsExecutable);
    }

    private static bool IsExecutable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try { return File.Exists(path); }
        catch { return false; }
    }
}
