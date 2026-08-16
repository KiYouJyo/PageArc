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

    public async Task<IFlowBookSource> OpenAsync(BookEntry book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        var candidates = _adapters.Where(x => x.CanOpen(book)).ToArray();
        if (candidates.Length == 0)
        {
            var format = BookFormatRegistry.Normalize(book.Format);
            throw new NotSupportedException($"No flow adapter is registered for {format}.");
        }

        Exception? lastFailure = null;
        foreach (var adapter in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await adapter.OpenAsync(book, cancellationToken);
            }
            catch (DrmProtectedEbookException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastFailure = ex;
                StartupDiagnostics.Log($"Flow adapter {adapter.GetType().Name} failed for {book.Format}; trying the next compatible adapter.", ex);
            }
        }

        throw lastFailure ?? new InvalidDataException($"Unable to open {book.Format} with the registered flow adapters.");
    }

    private static IEnumerable<IFlowBookAdapter> CreateDefaultAdapters()
    {
        yield return new EpubFlowAdapter();
        yield return new Fb2FlowAdapter();
        yield return new CalibreNormalizedFlowAdapter();
    }
}
