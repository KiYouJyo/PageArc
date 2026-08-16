using Xunit;

namespace PageArc.Tests;

public sealed class ReaderRegressionTests
{
    [Fact]
    public void ReaderLoadsNormalizedEpubChapterDirectlyWithNativeFallback()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml.cs"));

        Assert.Contains("BookWebView.NavigateToString(_currentChapter.Html);", code, StringComparison.Ordinal);
        Assert.DoesNotContain("BookWebView.Source = new Uri", code, StringComparison.Ordinal);
        Assert.Contains("SetVirtualHostNameToFolderMapping", code, StringComparison.Ordinal);
        Assert.Contains("switching EPUB reader to native compatibility mode", code, StringComparison.Ordinal);
        Assert.Contains("ShowNativeFallback", code, StringComparison.Ordinal);
        Assert.Contains("IsRenderedDocumentEmptyAsync", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderNormalizesCalibreEpub2SvgCoverLinksAndExtractsTextFallback()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Services", "EpubWebRenderer.cs"));
        Assert.Contains("xlink:href", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=", code, StringComparison.Ordinal);
        Assert.Contains("ExtractReadableText", code, StringComparison.Ordinal);
        Assert.Contains("ResolveInitialSpineIndex", code, StringComparison.Ordinal);
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
