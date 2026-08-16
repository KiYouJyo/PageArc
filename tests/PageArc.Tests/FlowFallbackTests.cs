using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class FlowFallbackTests
{
    [Fact]
    public async Task Engine_FallsBackWhenPreferredParserCannotHandleACompatibleFile()
    {
        var fallback = new SuccessfulAdapter();
        var engine = new FlowReaderEngine([new FailingAdapter(new InvalidDataException("compatibility")), fallback]);
        var book = new BookEntry { FilePath = "fixture.mobi", Format = "MOBI", Title = "Fixture" };

        await using var source = await engine.OpenAsync(book);
        Assert.Equal("fallback", source.Document.Title);
        Assert.True(fallback.OpenCalled);
    }

    [Fact]
    public async Task Engine_DoesNotFallbackAfterConfirmedDrm()
    {
        var fallback = new SuccessfulAdapter();
        var engine = new FlowReaderEngine([new FailingAdapter(new DrmProtectedEbookException("DRM")), fallback]);
        var book = new BookEntry { FilePath = "fixture.mobi", Format = "MOBI", Title = "Fixture" };

        await Assert.ThrowsAsync<DrmProtectedEbookException>(() => engine.OpenAsync(book));
        Assert.False(fallback.OpenCalled);
    }

    private sealed class FailingAdapter(Exception exception) : IFlowBookAdapter
    {
        public IReadOnlyCollection<string> Formats => ["MOBI"];
        public bool CanOpen(BookEntry book) => true;
        public Task<IFlowBookSource> OpenAsync(BookEntry book, CancellationToken cancellationToken = default) =>
            Task.FromException<IFlowBookSource>(exception);
    }

    private sealed class SuccessfulAdapter : IFlowBookAdapter
    {
        public bool OpenCalled { get; private set; }
        public IReadOnlyCollection<string> Formats => ["MOBI"];
        public bool CanOpen(BookEntry book) => true;
        public Task<IFlowBookSource> OpenAsync(BookEntry book, CancellationToken cancellationToken = default)
        {
            OpenCalled = true;
            return Task.FromResult<IFlowBookSource>(new Source());
        }
    }

    private sealed class Source : IFlowBookSource
    {
        public FlowDocument Document { get; } = new()
        {
            Format = "MOBI",
            Title = "fallback",
            Sections = [new FlowSection("1", "1", "text/html")]
        };

        public Task<FlowSectionContent> LoadSectionAsync(int sectionIndex, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FlowSectionContent("<p>fallback</p>", "fallback"));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
