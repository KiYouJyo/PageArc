using System.IO.Compression;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class ReaderStartupPerformanceTests
{
    [Fact]
    public void ReaderFirstPaint_DoesNotWaitForWholeBookPagination()
    {
        var root = FindRepoRoot();
        var code = File.ReadAllText(Path.Combine(root, "Pages", "ReaderPage.xaml.cs"));
        Assert.DoesNotContain("MeasureAndFreezePageMapAsync", code, StringComparison.Ordinal);
        var map = code.IndexOf("_pageMap = new FlowPageMap(_document);", StringComparison.Ordinal);
        var open = code.IndexOf("await NavigateToSectionAsync(requestedIndex", StringComparison.Ordinal);
        Assert.True(map >= 0 && open > map);
    }

    [Fact]
    public void RestoredReaderTabs_AreMaterializedOnFirstSelection()
    {
        var root = FindRepoRoot();
        var restore = File.ReadAllText(Path.Combine(root, "MainWindow.SessionRestore.cs"));
        var window = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        Assert.Contains("_pendingReaderBooks[session.Id] = book", restore, StringComparison.Ordinal);
        Assert.Contains("_pendingReaderBooks.TryGetValue(session.Id", window, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractionCache_ReusesUnchangedBookAndRefreshesModifiedSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "PageArcCacheTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var epub = Path.Combine(root, "book.epub");
        var output = Path.Combine(root, "extracted");
        try
        {
            using (var zip = ZipFile.Open(epub, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("META-INF/container.xml").Open()))
                    writer.Write("<container/>");
                using (var writer = new StreamWriter(zip.CreateEntry("OPS/chapter.xhtml").Open()))
                    writer.Write("source");
            }
            async Task Ensure()
            {
                using var zip = ZipFile.OpenRead(epub);
                await EpubExtractionCache.EnsureAsync(zip, epub, output);
            }
            await Ensure();
            var chapter = Path.Combine(output, "OPS", "chapter.xhtml");
            await File.WriteAllTextAsync(chapter, "locally changed");
            await Ensure();
            Assert.Equal("locally changed", await File.ReadAllTextAsync(chapter));

            File.SetLastWriteTimeUtc(epub, File.GetLastWriteTimeUtc(epub).AddSeconds(3));
            await Ensure();
            Assert.Equal("source", await File.ReadAllTextAsync(chapter));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EpubSectionModel_CarriesArchiveSizeForInitialPageEstimates()
    {
        var root = FindRepoRoot();
        var parser = File.ReadAllText(Path.Combine(root, "Services", "EpubParser.cs"));
        var adapter = File.ReadAllText(Path.Combine(root, "Services", "EpubFlowAdapter.cs"));
        Assert.Contains("item.MediaType, item.Size", adapter, StringComparison.Ordinal);
        Assert.Contains("?.Length ?? 0", parser, StringComparison.Ordinal);
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
