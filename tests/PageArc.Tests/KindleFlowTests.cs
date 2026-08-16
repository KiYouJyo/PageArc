using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class KindleFlowTests
{
    [Theory]
    [InlineData("MOBI", "fixture.mobi")]
    [InlineData("AZW3", "fixture.azw3")]
    public async Task BuiltInKindleAdapter_ProjectsRuntimeBookIntoFlowContract(string format, string fileName)
    {
        var runtime = new FakeKindleRuntime(format);
        var adapter = new MobiFlowAdapter(runtime);
        var book = new BookEntry { FilePath = fileName, Format = format, Title = "Fallback" };

        Assert.True(adapter.CanOpen(book));
        var source = await adapter.OpenAsync(book);
        Assert.Equal(format, source.Document.Format);
        Assert.Equal("Kindle fixture", source.Document.Title);
        Assert.Equal("Test Author", source.Document.Author);
        Assert.Equal("en", source.Document.Language);
        Assert.Equal(2, source.Document.Sections.Count);
        Assert.Equal(2, source.Document.Toc.Count);
        Assert.Equal(1, source.Document.Toc[1].SectionIndex);

        var section = await source.LoadSectionAsync(1);
        Assert.Contains("Section 2", section.Html, StringComparison.Ordinal);
        Assert.Equal("Section 2 plain text", section.PlainText);
        Assert.False(runtime.CloseCalled);

        await source.DisposeAsync();
        Assert.True(runtime.CloseCalled);
    }

    [Fact]
    public async Task RuntimeAdapter_CanTakePriorityOverLegacyNormalizationFallback()
    {
        var runtime = new FakeKindleRuntime("MOBI");
        var engine = new FlowReaderEngine([new NeverOpenAdapter()]);
        engine.RegisterAdapter(new MobiFlowAdapter(runtime), prefer: true);

        var book = new BookEntry { FilePath = "fixture.mobi", Format = "MOBI", Title = "Fixture" };
        await using var source = await engine.OpenAsync(book);
        Assert.Equal("Kindle fixture", source.Document.Title);
    }

    [Fact]
    public void VendoredKindleRuntime_IsPinnedAndPackagedLocally()
    {
        var root = FindRepoRoot();
        var pin = File.ReadAllText(Path.Combine(root, "ThirdParty", "foliate-js", "PIN.md"));
        var mobi = File.ReadAllText(Path.Combine(root, "ThirdParty", "foliate-js", "mobi.js"));
        var fflate = File.ReadAllText(Path.Combine(root, "ThirdParty", "foliate-js", "vendor", "fflate.js"));
        var project = File.ReadAllText(Path.Combine(root, "PageArc.csproj"));

        Assert.Contains("78914aef4466eb960965702401634c2cb348e9b1", pin, StringComparison.Ordinal);
        Assert.Contains("export class MOBI", mobi, StringComparison.Ordinal);
        Assert.Contains("unzlibSync", fflate, StringComparison.Ordinal);
        Assert.Contains("ThirdParty\\foliate-js\\mobi.js", project, StringComparison.Ordinal);
        Assert.DoesNotContain("cdn", pin, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeKindleRuntime(string format) : IKindleParserRuntime
    {
        public bool CloseCalled { get; private set; }

        public Task<KindleRuntimeBook> OpenAsync(BookEntry book, CancellationToken cancellationToken = default) =>
            Task.FromResult(new KindleRuntimeBook
            {
                Format = format,
                Title = "Kindle fixture",
                Author = "Test Author",
                Language = "en",
                Sections =
                [
                    new KindleRuntimeSection { OriginalIndex = 0, Id = "one", Size = 100 },
                    new KindleRuntimeSection { OriginalIndex = 1, Id = "two", Size = 120 }
                ],
                Toc =
                [
                    new KindleRuntimeTocItem { Title = "One", Href = "kindle:one", SectionIndex = 0 },
                    new KindleRuntimeTocItem { Title = "Two", Href = "kindle:two", SectionIndex = 1 }
                ]
            });

        public Task<KindleRuntimeSectionContent> LoadSectionAsync(int flowSectionIndex, CancellationToken cancellationToken = default) =>
            Task.FromResult(new KindleRuntimeSectionContent
            {
                Html = $"<html><body><p>Section {flowSectionIndex + 1}</p></body></html>",
                PlainText = $"Section {flowSectionIndex + 1} plain text"
            });

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            CloseCalled = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NeverOpenAdapter : IFlowBookAdapter
    {
        public IReadOnlyCollection<string> Formats => [];
        public bool CanOpen(BookEntry book) => false;
        public Task<IFlowBookSource> OpenAsync(BookEntry book, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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
