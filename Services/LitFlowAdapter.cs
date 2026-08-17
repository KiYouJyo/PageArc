using PageArc.Models;
using PageArc.Services.Conversion;

namespace PageArc.Services;

public sealed class LitFlowAdapter : IFlowBookAdapter
{
    private static readonly string[] AdapterFormats = ["LIT"];
    private readonly EbookConversionService _conversionService;

    public LitFlowAdapter(EbookConversionService? conversionService = null)
    {
        _conversionService = conversionService ?? new EbookConversionService();
    }

    public IReadOnlyCollection<string> Formats => AdapterFormats;

    public bool CanOpen(BookEntry book)
    {
        ArgumentNullException.ThrowIfNull(book);
        return string.Equals(ResolveFormat(book), "LIT", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IFlowBookSource> OpenAsync(BookEntry book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (!CanOpen(book)) throw new NotSupportedException($"The LIT flow adapter cannot open {book.Format}.");

        if (!_conversionService.CanConvert("LIT", "EPUB"))
        {
            throw new NotSupportedException(
                $"LIT reading requires a local conversion provider capable of LIT→EPUB. " +
                $"The default provider is calibre ebook-convert; install calibre or set {CalibreConversionProvider.EnvironmentVariable}. " +
                "PageArc does not modify the source file or attempt DRM removal.");
        }

        AppPaths.Ensure();
        var directory = Path.Combine(AppPaths.NormalizedBooksRoot, book.Id, "lit");
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
            await NormalizeAsync(book, normalizedPath, stampPath, expectedStamp, cancellationToken);

        if (!File.Exists(normalizedPath) || new FileInfo(normalizedPath).Length == 0)
            throw new InvalidDataException("PageArc could not prepare a readable copy of this LIT ebook.");

        var normalizedBook = new BookEntry
        {
            Id = $"{book.Id}-lit-normalized",
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
            StartupDiagnostics.Log($"Normalized LIT metadata enrichment failed for '{book.FilePath}'.", ex);
        }

        var inner = await new EpubFlowAdapter().OpenAsync(normalizedBook, cancellationToken);
        return new Source(inner);
    }

    private async Task NormalizeAsync(
        BookEntry book,
        string normalizedPath,
        string stampPath,
        string expectedStamp,
        CancellationToken cancellationToken)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "PageArc", "Normalize", book.Id);
        Directory.CreateDirectory(tempRoot);
        var tempPath = Path.Combine(tempRoot, $"lit-{Guid.NewGuid():N}.epub");
        try
        {
            var result = await _conversionService.ConvertAsync(
                new EbookConversionRequest(
                    book.FilePath,
                    "EPUB",
                    tempPath,
                    new EbookConversionOptions(true, true, true)),
                cancellationToken);

            if (result.IsDrmProtected)
                throw new DrmProtectedEbookException("This LIT ebook is DRM-protected and cannot be opened by PageArc.");

            if (!result.Success || !File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
            {
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                    StartupDiagnostics.Log($"LIT normalization provider failed for '{book.FilePath}': {result.ErrorMessage}");
                throw new InvalidDataException("PageArc could not convert this LIT ebook into a readable local copy.");
            }

            var targetDirectory = Path.GetDirectoryName(normalizedPath)!;
            Directory.CreateDirectory(targetDirectory);
            File.Move(tempPath, normalizedPath, overwrite: true);
            await File.WriteAllTextAsync(stampPath, expectedStamp, cancellationToken);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                if (Directory.Exists(tempRoot) && !Directory.EnumerateFileSystemEntries(tempRoot).Any()) Directory.Delete(tempRoot);
            }
            catch
            {
                // Temporary normalization cleanup is best-effort.
            }
        }
    }

    private static string ResolveFormat(BookEntry book)
    {
        var format = BookFormatRegistry.Normalize(book.Format);
        return string.IsNullOrWhiteSpace(format) ? BookFormatRegistry.FormatFromPath(book.FilePath) : format;
    }

    private sealed class Source : IFlowBookSource
    {
        private readonly IFlowBookSource _inner;

        public Source(IFlowBookSource inner)
        {
            _inner = inner;
            var document = inner.Document;
            Document = new FlowDocument
            {
                Format = "LIT",
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
