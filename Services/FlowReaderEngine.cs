using PageArc.Models;

namespace PageArc.Services;

public sealed class FlowReaderEngine
{
    private readonly List<IFlowBookAdapter> _adapters;

    public FlowReaderEngine(IEnumerable<IFlowBookAdapter>? adapters = null)
    {
        _adapters = (adapters ?? CreateDefaultAdapters()).ToList();
    }

    public IReadOnlyList<string> ReadableFormats => _adapters
        .SelectMany(x => x.Formats)
        .Select(BookFormatRegistry.Normalize)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public void RegisterAdapter(IFlowBookAdapter adapter, bool prefer = true)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _adapters.RemoveAll(existing => ReferenceEquals(existing, adapter));
        if (prefer) _adapters.Insert(0, adapter);
        else _adapters.Add(adapter);
    }

    public bool CanOpen(BookEntry book) => _adapters.Any(adapter => adapter.CanOpen(book));

    public Task<IFlowBookSource> OpenAsync(BookEntry book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        var adapter = _adapters.FirstOrDefault(x => x.CanOpen(book));
        if (adapter is null)
        {
            var format = BookFormatRegistry.Normalize(book.Format);
            throw new NotSupportedException($"No flow adapter is registered for {format}.");
        }

        return adapter.OpenAsync(book, cancellationToken);
    }

    private static IEnumerable<IFlowBookAdapter> CreateDefaultAdapters()
    {
        yield return new EpubFlowAdapter();
        yield return new Fb2FlowAdapter();
        yield return new CalibreNormalizedFlowAdapter();
    }
}
