using PageArc.Models;

namespace PageArc.Services;

public interface IFlowBookSource : IAsyncDisposable
{
    FlowDocument Document { get; }
    Task<FlowSectionContent> LoadSectionAsync(int sectionIndex, CancellationToken cancellationToken = default);
}

public interface IFlowBookAdapter
{
    IReadOnlyCollection<string> Formats { get; }
    bool CanOpen(BookEntry book);
    Task<IFlowBookSource> OpenAsync(BookEntry book, CancellationToken cancellationToken = default);
}
