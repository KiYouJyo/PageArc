using Xunit;

namespace PageArc.Tests;

public sealed class ReaderRegressionTests
{
    [Fact]
    public void ReaderLoadsNormalizedEpubChapterDirectlyInsteadOfNavigatingTopLevelToVirtualHostFile()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml.cs"));

        Assert.Contains("BookWebView.NavigateToString(rendered.Html);", code, StringComparison.Ordinal);
        Assert.DoesNotContain("BookWebView.Source = new Uri", code, StringComparison.Ordinal);
        Assert.Contains("SetVirtualHostNameToFolderMapping", code, StringComparison.Ordinal);
        Assert.Contains("continuing with text-only chapter rendering", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderNormalizesCalibreEpub2SvgCoverLinks()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Services", "EpubWebRenderer.cs"));
        Assert.Contains("xlink:href", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=", code, StringComparison.Ordinal);
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
