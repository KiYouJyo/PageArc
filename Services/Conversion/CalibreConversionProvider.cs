using System.Diagnostics;
using PageArc.Models;

namespace PageArc.Services.Conversion;

public sealed class CalibreConversionProvider : IEbookConversionProvider
{
    public const string EnvironmentVariable = "PAGEARC_EBOOK_CONVERT";
    private readonly string? _executablePath;

    public CalibreConversionProvider(string? executablePath = null)
    {
        _executablePath = string.IsNullOrWhiteSpace(executablePath) ? ResolveExecutablePath() : executablePath;
    }

    public string Id => "calibre-ebook-convert";
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_executablePath) && File.Exists(_executablePath);

    public bool CanConvert(string inputFormat, string outputFormat)
    {
        var input = BookFormatRegistry.Normalize(inputFormat);
        var output = BookFormatRegistry.Normalize(outputFormat);
        return BookFormatRegistry.IsRequired(input)
            && BookFormatRegistry.IsRequired(output)
            && !string.Equals(input, output, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<EbookConversionResult> ConvertAsync(EbookConversionRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return EbookConversionResult.Failed("calibre ebook-convert is not installed or configured.");
        if (!File.Exists(request.InputPath))
            return EbookConversionResult.Failed("The source ebook does not exist.");

        var inputFormat = BookFormatRegistry.FormatFromPath(request.InputPath);
        var outputFormat = BookFormatRegistry.Normalize(request.OutputFormat);
        if (!CanConvert(inputFormat, outputFormat))
            return EbookConversionResult.Failed($"calibre provider cannot convert {inputFormat} to {outputFormat}.");

        var options = request.Options ?? new EbookConversionOptions();
        if (!options.KeepMetadata || !options.KeepCover || !options.KeepTableOfContents)
        {
            return EbookConversionResult.Failed(
                "The first PageArc calibre provider supports preservation mode only. Metadata, cover and table-of-contents stripping will be added behind explicit format-aware options.");
        }

        var outputPath = request.OutputPath;
        if (string.IsNullOrWhiteSpace(outputPath))
            outputPath = EbookConversionService.CreateOutputPath(request.InputPath, outputFormat);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(request.InputPath);
        startInfo.ArgumentList.Add(outputPath);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start()) return EbookConversionResult.Failed("Failed to start calibre ebook-convert.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Process is already terminating.
                }
            });

            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode == 0 && File.Exists(outputPath))
                return EbookConversionResult.Completed(outputPath);

            var details = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            if (string.IsNullOrWhiteSpace(details)) details = $"ebook-convert exited with code {process.ExitCode}.";
            var drm = details.Contains("DRM", StringComparison.OrdinalIgnoreCase)
                || details.Contains("encrypted", StringComparison.OrdinalIgnoreCase);
            return EbookConversionResult.Failed(details.Trim(), drm);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return EbookConversionResult.Failed(ex.Message);
        }
    }

    public static string? ResolveExecutablePath()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (IsExecutable(configured)) return Path.GetFullPath(configured!);

        var candidates = new List<string>();
        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            if (!string.IsNullOrWhiteSpace(root)) candidates.Add(Path.Combine(root, "Calibre2", "ebook-convert.exe"));
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            candidates.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(directory => Path.Combine(directory.Trim().Trim('"'), "ebook-convert.exe")));
        }

        return candidates.FirstOrDefault(IsExecutable);
    }

    private static bool IsExecutable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try { return File.Exists(path); }
        catch { return false; }
    }
}
