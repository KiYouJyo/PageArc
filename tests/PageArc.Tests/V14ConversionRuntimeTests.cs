using PageArc.Services;
using PageArc.Services.Conversion;
using Xunit;

namespace PageArc.Tests;

public sealed class V14ConversionRuntimeTests
{
    [Fact]
    public void BaseApplication_NoLongerPackagesBundledCalibre()
    {
        var root = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "PageArc.csproj"));

        Assert.DoesNotContain("ThirdParty\\calibre\\runtime", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PageArcBundledConversionProvider", project, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "Services", "Conversion", "PageArcBundledConversionProvider.cs")));
        Assert.False(File.Exists(Path.Combine(root, "eng", "prepare-calibre-runtime.ps1")));
    }

    [Fact]
    public void ManagedRuntime_IsPinnedToSeparateRepositoryRelease()
    {
        Assert.Equal("9.13.0-pagearc.1", ConversionRuntimeManager.PackageVersion);
        Assert.Equal("9.13.0", ConversionRuntimeManager.CalibreVersion);
        Assert.Equal(282915121, ConversionRuntimeManager.ExpectedArchiveSize);
        Assert.Equal(
            "1d223227254d6dfacc8f5645caf3cba26434e129cf5bb65decb0a121a61b5322",
            ConversionRuntimeManager.ExpectedArchiveSha256);
        Assert.Contains("KiYouJyo/PageArc.ConversionRuntime", ConversionRuntimeManager.ManifestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("v9.13.0-pagearc.1", ConversionRuntimeManager.ManifestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.EndsWith("/runtime-manifest.json", ConversionRuntimeManager.ManifestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.EndsWith("/PageArc.ConversionRuntime-win-x64.zip", ConversionRuntimeManager.ArchiveUri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void ManagedRuntime_InstallsOutsideApplicationPackage()
    {
        var manager = new ConversionRuntimeManager(new HttpClient(new OfflineHandler()));
        Assert.StartsWith(AppPaths.ConversionRuntimesRoot, manager.InstallRoot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ConversionRuntimeManager.PackageVersion, manager.InstallRoot, StringComparison.Ordinal);
        Assert.EndsWith(
            Path.Combine("runtime", "ebook-convert.exe"),
            manager.ExecutablePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            Path.Combine("ThirdParty", "calibre"),
            manager.ExecutablePath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultConversionService_UsesSystemCalibreThenManagedOnDemandProvider()
    {
        var service = new EbookConversionService();

        Assert.Equal(2, service.Providers.Count);
        Assert.IsType<CalibreConversionProvider>(service.Providers[0]);
        Assert.IsType<PageArcManagedConversionProvider>(service.Providers[1]);
        Assert.Equal(
            $"pagearc-managed-calibre-{ConversionRuntimeManager.PackageVersion}",
            service.Providers[1].Id);
    }

    [Fact]
    public void V14VersionContract_IsLightReaderRelease()
    {
        var root = FindRepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "PageArc.csproj"));
        var githubManifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));
        var acceptanceManifest = File.ReadAllText(Path.Combine(root, "Packaging", "PageArc.Package.appxmanifest"));
        var storeManifest = File.ReadAllText(Path.Combine(root, "Package.Store.appxmanifest"));

        Assert.Contains("<Version>1.4.0</Version>", project, StringComparison.Ordinal);
        Assert.Contains("<ApplicationDisplayVersion>1.4</ApplicationDisplayVersion>", project, StringComparison.Ordinal);
        Assert.Contains("Version=\"1.4.0.0\"", githubManifest, StringComparison.Ordinal);
        Assert.Contains("Version=\"1.4.0.0\"", acceptanceManifest, StringComparison.Ordinal);
        Assert.Contains("Version=\"2026.904.140.0\"", storeManifest, StringComparison.Ordinal);
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "PageArc.csproj"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("PageArc repository root not found.");
    }
}
