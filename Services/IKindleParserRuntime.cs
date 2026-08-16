using PageArc.Models;

namespace PageArc.Services;

public interface IKindleParserRuntime : IAsyncDisposable
{
    Task<KindleRuntimeBook> OpenAsync(BookEntry book, CancellationToken cancellationToken = default);
    Task<KindleRuntimeSectionContent> LoadSectionAsync(int flowSectionIndex, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
}
