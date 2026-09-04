using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace PageArc.Services.Conversion;

public sealed record ConversionRuntimeManifest(
    int SchemaVersion,
    string RuntimeId,
    string PackageVersion,
    string CalibreVersion,
    string MinimumPageArcVersion,
    string Platform,
    string Architecture,
    string ArchiveFileName,
    long ArchiveSize,
    string Sha256,
    string ExecutableRelativePath,
    string SourceFileName);

public readonly record struct ConversionRuntimeProgress(
    string Stage,
    long BytesTransferred,
    long? TotalBytes)
{
    public double Fraction => TotalBytes is > 0
        ? Math.Clamp((double)BytesTransferred / TotalBytes.Value, 0d, 1d)
        : 0d;
}

public sealed record ConversionRuntimeStatus(
    bool IsSupported,
    bool IsInstalled,
    string PackageVersion,
    string CalibreVersion,
    string? ExecutablePath,
    long InstalledBytes);

/// <summary>
/// Owns PageArc's optional conversion runtime lifecycle.
/// The runtime is distributed from KiYouJyo/PageArc.ConversionRuntime and is never embedded in the PageArc MSIX.
/// </summary>
public sealed class ConversionRuntimeManager
{
    public const string RuntimeId = "pagearc-calibre";
    public const string PackageVersion = "9.13.0-pagearc.1";
    public const string CalibreVersion = "9.13.0";
    public const string ReleaseTag = "v9.13.0-pagearc.1";
    public const string ArchiveFileName = "PageArc.ConversionRuntime-win-x64.zip";
    public const string ExecutableRelativePath = "runtime/ebook-convert.exe";
    public const long ExpectedArchiveSize = 282915121;
    public const string ExpectedArchiveSha256 = "1d223227254d6dfacc8f5645caf3cba26434e129cf5bb65decb0a121a61b5322";

    public static readonly Uri ManifestUri =
        new($"https://github.com/KiYouJyo/PageArc.ConversionRuntime/releases/download/{ReleaseTag}/runtime-manifest.json");

    public static readonly Uri ArchiveUri =
        new($"https://github.com/KiYouJyo/PageArc.ConversionRuntime/releases/download/{ReleaseTag}/{ArchiveFileName}");

    private static readonly SemaphoreSlim InstallGate = new(1, 1);
    private readonly HttpClient _client;

    public ConversionRuntimeManager(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
    }

    public bool IsSupported =>
        OperatingSystem.IsWindows()
        && RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.Arm64;

    public string InstallRoot => Path.Combine(AppPaths.ConversionRuntimesRoot, PackageVersion, "win-x64");
    public string ExecutablePath => Path.Combine(InstallRoot, ExecutableRelativePath.Replace('/', Path.DirectorySeparatorChar));

    public bool IsInstalled
    {
        get
        {
            try
            {
                return IsSupported && File.Exists(ExecutablePath) && new FileInfo(ExecutablePath).Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public ConversionRuntimeStatus GetStatus()
    {
        long bytes = 0;
        if (Directory.Exists(InstallRoot))
        {
            try
            {
                bytes = Directory.EnumerateFiles(InstallRoot, "*", SearchOption.AllDirectories)
                    .Sum(path => new FileInfo(path).Length);
            }
            catch
            {
                bytes = 0;
            }
        }

        return new ConversionRuntimeStatus(
            IsSupported,
            IsInstalled,
            PackageVersion,
            CalibreVersion,
            IsInstalled ? ExecutablePath : null,
            bytes);
    }

    public async Task<string> EnsureInstalledAsync(
        IProgress<ConversionRuntimeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException("The PageArc conversion runtime is currently distributed for Windows x64-compatible systems only.");
        if (IsInstalled)
            return ExecutablePath;

        await InstallGate.WaitAsync(cancellationToken);
        try
        {
            if (IsInstalled)
                return ExecutablePath;

            AppPaths.Ensure();
            progress?.Report(new ConversionRuntimeProgress("manifest", 0, null));
            var manifest = await DownloadAndValidateManifestAsync(cancellationToken);

            var downloadPath = Path.Combine(AppPaths.RuntimeDownloadsRoot, $"{PackageVersion}-{Guid.NewGuid():N}.zip.partial");
            var stagingRoot = Path.Combine(AppPaths.ConversionRuntimesRoot, $".staging-{PackageVersion}-{Guid.NewGuid():N}");
            try
            {
                await DownloadArchiveAsync(manifest, downloadPath, progress, cancellationToken);
                await VerifyArchiveAsync(downloadPath, manifest, cancellationToken);

                progress?.Report(new ConversionRuntimeProgress("extract", 0, manifest.ArchiveSize));
                ExtractArchiveSafely(downloadPath, stagingRoot, cancellationToken);

                var stagedExecutable = Path.Combine(
                    stagingRoot,
                    manifest.ExecutableRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(stagedExecutable) || new FileInfo(stagedExecutable).Length == 0)
                    throw new InvalidDataException("The downloaded conversion runtime does not contain a valid ebook-convert executable.");

                await ValidateExecutableAsync(stagedExecutable, cancellationToken);

                var parent = Path.GetDirectoryName(InstallRoot)!;
                Directory.CreateDirectory(parent);
                if (Directory.Exists(InstallRoot))
                    Directory.Delete(InstallRoot, recursive: true);
                Directory.Move(stagingRoot, InstallRoot);

                var installedManifest = Path.Combine(InstallRoot, "pagearc-runtime-manifest.json");
                await File.WriteAllTextAsync(
                    installedManifest,
                    JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
                    cancellationToken);

                progress?.Report(new ConversionRuntimeProgress("complete", manifest.ArchiveSize, manifest.ArchiveSize));
                return ExecutablePath;
            }
            finally
            {
                TryDeleteFile(downloadPath);
                TryDeleteDirectory(stagingRoot);
            }
        }
        finally
        {
            InstallGate.Release();
        }
    }

    public void RemoveInstalledRuntime()
    {
        try
        {
            if (Directory.Exists(InstallRoot))
                Directory.Delete(InstallRoot, recursive: true);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Conversion runtime removal failed", ex);
            throw;
        }
    }

    private async Task<ConversionRuntimeManifest> DownloadAndValidateManifestAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ManifestUri);
        request.Headers.UserAgent.ParseAdd("PageArc/1.4");
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var manifest = JsonSerializer.Deserialize<ConversionRuntimeManifest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The conversion runtime manifest is empty.");

        ValidateManifest(manifest);
        return manifest;
    }

    private static void ValidateManifest(ConversionRuntimeManifest manifest)
    {
        if (manifest.SchemaVersion != 1
            || !string.Equals(manifest.RuntimeId, RuntimeId, StringComparison.Ordinal)
            || !string.Equals(manifest.PackageVersion, PackageVersion, StringComparison.Ordinal)
            || !string.Equals(manifest.CalibreVersion, CalibreVersion, StringComparison.Ordinal)
            || !string.Equals(manifest.Platform, "windows", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(manifest.Architecture, "x64", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(manifest.ArchiveFileName, ArchiveFileName, StringComparison.Ordinal)
            || !string.Equals(manifest.ExecutableRelativePath, ExecutableRelativePath, StringComparison.Ordinal)
            || manifest.ArchiveSize != ExpectedArchiveSize
            || !string.Equals(manifest.Sha256, ExpectedArchiveSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The conversion runtime manifest does not match PageArc's pinned runtime contract.");
        }
    }

    private async Task DownloadArchiveAsync(
        ConversionRuntimeManifest manifest,
        string destinationPath,
        IProgress<ConversionRuntimeProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ArchiveUri);
        request.Headers.UserAgent.ParseAdd("PageArc/1.4");
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? manifest.ArchiveSize;
        if (total > 0 && total != manifest.ArchiveSize)
            throw new InvalidDataException($"Conversion runtime size mismatch before download: expected {manifest.ArchiveSize}, remote reports {total}.");

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 256, useAsync: true);
        var buffer = new byte[1024 * 256];
        long copied = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
            progress?.Report(new ConversionRuntimeProgress("download", copied, manifest.ArchiveSize));
        }

        await target.FlushAsync(cancellationToken);
        if (copied != manifest.ArchiveSize)
            throw new InvalidDataException($"Conversion runtime download is incomplete: expected {manifest.ArchiveSize} bytes, received {copied}.");
    }

    private static async Task VerifyArchiveAsync(
        string path,
        ConversionRuntimeManifest manifest,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 256, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        if (!string.Equals(actual, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Conversion runtime SHA-256 verification failed.");
    }

    private static void ExtractArchiveSafely(string archivePath, string destinationRoot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationRoot);
        var root = Path.GetFullPath(destinationRoot) + Path.DirectorySeparatorChar;
        using var stream = File.OpenRead(archivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(destinationRoot, relative));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Conversion runtime archive contains an unsafe path.");

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = entry.Open();
            using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    private static async Task ValidateExecutableAsync(string executablePath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("--version");

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidDataException("The downloaded conversion runtime could not be started.");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = string.Concat(await stdout, " ", await stderr);
        if (process.ExitCode != 0 || !output.Contains("calibre 9.13.0", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded conversion runtime failed its executable validation.");
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }
}
