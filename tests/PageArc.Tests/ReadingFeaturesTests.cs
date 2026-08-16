using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class ReadingFeaturesTests
{
    [Fact]
    public async Task FlowSearch_FindsMatchesAcrossSectionsWithStableLocators()
    {
        await using var source = new FakeSource(
            ["Alpha street edge.", "A STREET is more than a route. Another street appears."],
            [new FlowTocItem("First", "one", 0), new FlowTocItem("Second", "two", 1)]);

        var results = await new FlowSearchService().SearchAsync(source, "street");

        Assert.Equal(3, results.Count);
        Assert.Equal(0, results[0].SectionIndex);
        Assert.Equal("First", results[0].ChapterTitle);
        Assert.Equal(1, results[1].SectionIndex);
        Assert.Equal("Second", results[1].ChapterTitle);
        Assert.InRange(results[2].Fraction, 0, 1);
        Assert.Contains("street", results[2].Snippet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadingData_RoundTripsBookmarksAndAnnotations()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"pagearc-reading-{Guid.NewGuid():N}");
        var file = Path.Combine(directory, "reading-data.json");
        try
        {
            var service = new ReadingDataService(file);
            service.Load();
            var locator = new FlowContentLocator(3, 0.42, "anchor", "quoted text");
            var bookmark = service.ToggleBookmark("book-1", locator, "Chapter 4", "A useful passage");
            Assert.NotNull(bookmark);

            var annotation = service.SaveAnnotation(new ReaderAnnotation
            {
                BookId = "book-1",
                Locator = locator,
                ChapterTitle = "Chapter 4",
                Quote = "quoted text",
                Note = "note",
                HighlightColor = "blue"
            });
            Assert.False(string.IsNullOrWhiteSpace(annotation.Id));

            var reloaded = new ReadingDataService(file);
            reloaded.Load();
            var bookmarks = reloaded.GetBookmarks("book-1");
            var annotations = reloaded.GetAnnotations("book-1");
            Assert.Single(bookmarks);
            Assert.Single(annotations);
            Assert.Equal(0.42, bookmarks[0].Locator.Fraction, 6);
            Assert.Equal("note", annotations[0].Note);
            Assert.True(reloaded.HasBookmark("book-1", new FlowContentLocator(3, 0.425)));

            Assert.Null(reloaded.ToggleBookmark("book-1", new FlowContentLocator(3, 0.425), "Chapter 4", "same location"));
            Assert.Empty(reloaded.GetBookmarks("book-1"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private sealed class FakeSource : IFlowBookSource
    {
        private readonly IReadOnlyList<string> _sections;

        public FakeSource(IReadOnlyList<string> sections, IReadOnlyList<FlowTocItem> toc)
        {
            _sections = sections;
            Document = new FlowDocument
            {
                Format = "TEST",
                Title = "Search fixture",
                Sections = sections.Select((text, index) => new FlowSection($"s{index}", $"s{index}", "text/plain", text.Length)).ToArray(),
                Toc = toc
            };
        }

        public FlowDocument Document { get; }

        public Task<FlowSectionContent> LoadSectionAsync(int sectionIndex, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = _sections[sectionIndex];
            return Task.FromResult(new FlowSectionContent($"<p>{text}</p>", text));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
