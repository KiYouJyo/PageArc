using System.Net;
using System.Text;
using PageArc.Services.Conversion;
using Xunit;

namespace PageArc.Tests;

public sealed class V14ExtensionRuntimeManagementTests
{
    [Fact]
    public void AboutPage_HasExtensionUpdateManagementCard()
    {
        var root = FindRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "Pages", "AboutPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "Pages", "AboutPage.xaml.cs"));

        Assert.Contains("x:Name=\"ExtensionManagementHeading\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ConversionRuntimeNameText\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RuntimeInstalledVersionText\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RuntimeAvailableVersionText\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RuntimeDownloadProgress\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ConversionRuntimeStateBadge", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CheckRuntimeUpdatesButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RuntimeActionButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"RemoveRuntimeButton\"", xaml, StringComparison.Ordinal);

        Assert.Contains("CheckRuntimeUpdates_Click", code, StringComparison.Ordinal);
        Assert.Contains("RuntimeAction_Click", code, StringComparison.Ordinal);
        Assert.Contains("RemoveRuntime_Click", code, StringComparison.Ordinal);
        Assert.Contains("PageArc.ConversionRuntime", code, StringComparison.Ordinal);
        Assert.Contains("下载并更新", code, StringComparison.Ordinal);
        Assert.Contains("下载并安装", code, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeDownloadState_PersistsOutsideAboutPageInstance()
    {
        var root = FindRepoRoot();
        var about = File.ReadAllText(Path.Combine(root, "Pages", "AboutPage.xaml.cs"));
        var conversion = File.ReadAllText(Path.Combine(root, "Pages", "ConversionPage.xaml.cs"));
        var reader = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml.cs"));
        var provider = File.ReadAllText(Path.Combine(root, "Services", "Conversion", "PageArcManagedConversionProvider.cs"));
        var manager = File.ReadAllText(Path.Combine(root, "Services", "Conversion", "ConversionRuntimeManager.cs"));

        Assert.Contains("ConversionRuntimeManager.Shared", about, StringComparison.Ordinal);
        Assert.Contains("OperationStateChanged", about, StringComparison.Ordinal);
        Assert.Contains("AboutPage_Loaded", about, StringComparison.Ordinal);
        Assert.Contains("AboutPage_Unloaded", about, StringComparison.Ordinal);
        Assert.Contains("ApplyRuntimeOperationState(_runtimeManager.OperationState)", about, StringComparison.Ordinal);
        Assert.Contains("ConversionRuntimeManager.Shared", conversion, StringComparison.Ordinal);
        Assert.Contains("ConversionRuntimeManager.Shared", reader, StringComparison.Ordinal);
        Assert.Contains("ConversionRuntimeManager.Shared", provider, StringComparison.Ordinal);
        Assert.Contains("public ConversionRuntimeOperationState OperationState", manager, StringComparison.Ordinal);
        Assert.Contains("public static ConversionRuntimeManager Shared", manager, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeRemovalDialog_UsesCompactStandardPrimaryActionLayout()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "AboutPage.xaml.cs"));

        Assert.Contains("MaxWidth = 520", code, StringComparison.Ordinal);
        Assert.Contains("TextWrapping = TextWrapping.Wrap", code, StringComparison.Ordinal);
        Assert.Contains("DefaultButton = ContentDialogButton.Primary", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultButton = ContentDialogButton.Close", ExtractRemoveRuntimeMethod(code), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeManager_ChecksLatestCompatibleReleaseIndependently()
    {
        const string manifestJson = """
        {
          "schemaVersion": 1,
          "runtimeId": "pagearc-calibre",
          "packageVersion": "9.14.0-pagearc.1",
          "calibreVersion": "9.14.0",
          "minimumPageArcVersion": "1.4.0",
          "platform": "windows",
          "architecture": "x64",
          "archiveFileName": "PageArc.ConversionRuntime-win-x64.zip",
          "archiveSize": 2097152,
          "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          "executableRelativePath": "runtime/ebook-convert.exe",
          "sourceFileName": "calibre-9.14.0.tar.xz"
        }
        """;

        var releaseJson = """
        {
          "tag_name": "v9.14.0-pagearc.1",
          "draft": false,
          "prerelease": false,
          "published_at": "2026-09-04T00:00:00Z",
          "assets": [
            {
              "name": "runtime-manifest.json",
              "size": 1000,
              "browser_download_url": "https://example.test/runtime-manifest.json"
            },
            {
              "name": "PageArc.ConversionRuntime-win-x64.zip",
              "size": 2097152,
              "browser_download_url": "https://example.test/PageArc.ConversionRuntime-win-x64.zip"
            }
          ]
        }
        """;

        using var client = new HttpClient(new RuntimeReleaseHandler(releaseJson, manifestJson));
        var manager = new ConversionRuntimeManager(client);
        var result = await manager.CheckForUpdatesAsync();

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.NotNull(result.LatestCompatibleRelease);
        Assert.Equal("9.14.0-pagearc.1", result.LatestCompatibleRelease!.Manifest.PackageVersion);
        Assert.Equal("9.14.0", result.LatestCompatibleRelease.Manifest.CalibreVersion);
        Assert.True(result.UpdateAvailable || !result.LocalStatus.IsInstalled);
    }

    [Fact]
    public void AboutLicenseText_DescribesDetachedRuntime()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "AboutPage.xaml.cs"));

        Assert.Contains("已从基础安装包剥离", code, StringComparison.Ordinal);
        Assert.Contains("PageArc.ConversionRuntime", code, StringComparison.Ordinal);
        Assert.DoesNotContain("官方 x64 包同时内置 calibre", code, StringComparison.Ordinal);
    }

    private sealed class RuntimeReleaseHandler(string releaseJson, string manifestJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (uri.Contains("/releases/latest", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(releaseJson, Encoding.UTF8, "application/json")
                });
            }

            if (uri.EndsWith("/runtime-manifest.json", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(manifestJson, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static string ExtractRemoveRuntimeMethod(string code)
    {
        var start = code.IndexOf("private async void RemoveRuntime_Click", StringComparison.Ordinal);
        var end = code.IndexOf("private void RefreshRuntimeCard", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return code[start..end];
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
