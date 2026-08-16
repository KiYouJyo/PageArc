using PageArc.Models;
using PageArc.Services.Conversion;

namespace PageArc.Services;

public sealed class CalibreNormalizedFlowAdapter : IFlowBookAdapter
{
    private static readonly string[] AdapterFormats = ["MOBI", "AZW3"];
    private readonly EbookConversionService _conversionService;

    public CalibreNormalizedFlowAdapter(EbookConversionService? conversionService = null)
    {
        _conversionService = conversionService ?? new EbookConversionService();
    }

    public IReadOnlyCollection<string> Formats => AdapterFormats;

    public bool CanOpen(BookEntry book)
    {
        var format = ResolveFormat(book);
        return AdapterFormats.Contains(format, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IFlowBookSource> OpenAsync(BookEntry book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        var format = ResolveFormat(book);
        if (!AdapterFormats.Contains(format, StringComparer.OrdinalIgnoreCase))
            throw new NotSupportedException($"The normalized flow adapter cannot open {format}.");

        if (!_conversionService.CanConvert(format, "EPUB"))
        {
            throw new NotSupportedException(
                $"{format} compatibility fallback requires a configured calibre ebook-convert provider. " +
                $"Install calibre or set {CalibreConversionProvider.EnvironmentVariable}; PageArc never attempts DRM removal.");
        }

        AppPaths.Ensure();
        var directory = Path.Combine(AppPaths.NormalizedBooksRoot, book.Id, format.ToLowerInvariant());
        Directory.CreateDirectory(directory);
        var normalizedPath = Path.Combine(directory, "source.epub");
        var sourceInfo = new FileInfo(book.FilePath);
        var stampPath = Path.Combine(directory, "source.stamp");
        var expectedStamp = $"{sourceInfo.Length}:{sourceInfo.LastWriteTimeUtc.Ticks}";
        var cachedStamp = File.Exists(stampPath) ? await File.ReadAllTextAsync(stampPath, cancellationToken) : string.Empty;

        var needsRefresh = !File.Exists(normalizedPath)
            || new FileInfo(normalizedPath).Length == 0
            || !string.Equals(cachedStamp, expectedStamp, StringComparison.Ordinal);

        if (needsRefresh)
        {
            if (File.Exists(normalizedPath)) File.Delete(normalizedPath);
            var result = await _conversionService.ConvertAsync(
                new EbookConversionRequest(
                    book.FilePath,
                    "EPUB",
                    normalizedPath,
                    new EbookConversionOptions(true, true, true)),
                cancellationToken);

            if (result.IsDrmProtected)
                throw new DrmProtectedEbookException(result.ErrorMessage ?? $"This {format} ebook is DRM-protected and cannot be opened by PageArc.");
            if (!result.Success || string.IsNullOrWhiteSpace(result.OutputPath) || !File.Exists(result.OutputPath))
                throw new InvalidDataException(result.ErrorMessage ?? $"Failed to normalize {format} to EPUB.");

            await File.WriteAllTextAsync(stampPath, expectedStamp, cancellationToken);
        }

        var normalizedBook = new BookEntry
        {
            Id = $"{book.Id}-{format.ToLowerInvariant()}-normalized",
            FilePath = normalizedPath,
            Format = "EPUB",
            Title = book.Title,
            Author = book.Author,
            FileSize = new FileInfo(normalizedPath).Length,
            Progress = book.Progress,
            SpineIndex = book.SpineIndex,
            SectionFraction = book.SectionFraction
        };

        try
        {
            var metadata = await BookMetadataService.ReadAsync(normalizedBook, cancellationToken);
            if (!string.IsNullOrWhiteSpace(metadata.Title)) book.Title = metadata.Title;
            if (!string.IsNullOrWhiteSpace(metadata.Author)) book.Author = metadata.Author;
            if (!string.IsNullOrWhiteSpace(metadata.Language)) book.Language = metadata.Language;
            if (!string.IsNullOrWhiteSpace(metadata.Publisher)) book.Publisher = metadata.Publisher;
            if (!string.IsNullOrWhiteSpace(metadata.Description)) book.Description = metadata.Description;
            if (!string.IsNullOrWhiteSpace(metadata.CoverPath)) book.CoverPath = metadata.CoverPath;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StartupDiagnostics.Log($"Normalized {format} metadata enrichment failed for '{book.FilePath}'.", ex);
        }

        var inner = await new EpubFlowAdapter().OpenAsync(normalizedBook, cancellationToken);
        return new NormalizedSource(inner, format);
    }

    private static string ResolveFormat(BookEntry book)
    {
        var format = BookFormatRegistry.Normalize(book.Format);
        return string.IsNullOrWhiteSpace(format) ? BookFormatRegistry.FormatFromPath(book.FilePath) : format;
    }

    private sealed class NormalizedSource : IFlowBookSource
    {
        private readonly IFlowBookSource _inner;

        public NormalizedSource(IFlowBookSource inner, string originalFormat)
        {
            _inner = inner;
            var document = inner.Document;
            Document = new FlowDocument
            {
                Format = originalFormat,
                Title = document.Title,
                Author = document.Author,
                Language = document.Language,
                CoverHref = document.CoverHref,
                CacheRoot = document.CacheRoot,
                Sections = document.Sections,
                Toc = document.Toc
            };
        }

        public FlowDocument Document { get; }

        public Task<FlowSectionContent> LoadSectionAsync(int sectionIndex, CancellationToken cancellationToken = default) =>
            _inner.LoadSectionAsync(sectionIndex, cancellationToken);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
