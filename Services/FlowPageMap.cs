using PageArc.Models;

namespace PageArc.Services;

/// <summary>
/// Provides a stable logical-page map for reflowable books without pretending that EPUB-style
/// documents contain fixed printed pages. The estimate is derived from each flow section's source
/// size and is used only for navigation UI; the canonical saved position remains section+fraction.
/// </summary>
public sealed class FlowPageMap
{
    private const double BytesPerLogicalPage = 3500d;
    private readonly int[] _pagesPerSection;
    private readonly int[] _sectionStarts;

    public FlowPageMap(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _pagesPerSection = document.Sections
            .Select(section => Math.Max(1, (int)Math.Ceiling(Math.Max(1, section.Size) / BytesPerLogicalPage)))
            .ToArray();
        if (_pagesPerSection.Length == 0) _pagesPerSection = [1];

        _sectionStarts = new int[_pagesPerSection.Length];
        var running = 0;
        for (var i = 0; i < _pagesPerSection.Length; i++)
        {
            _sectionStarts[i] = running;
            running += _pagesPerSection[i];
        }
        TotalPages = Math.Max(1, running);
    }

    public int TotalPages { get; }

    public int GetPage(int sectionIndex, double fraction)
    {
        sectionIndex = Math.Clamp(sectionIndex, 0, _pagesPerSection.Length - 1);
        fraction = Math.Clamp(fraction, 0, 1);
        var sectionPages = _pagesPerSection[sectionIndex];
        var within = Math.Min(sectionPages - 1, (int)Math.Floor(fraction * sectionPages));
        return Math.Clamp(_sectionStarts[sectionIndex] + within + 1, 1, TotalPages);
    }

    public FlowContentLocator LocatePage(int page)
    {
        page = Math.Clamp(page, 1, TotalPages);
        var zeroBased = page - 1;
        var sectionIndex = _pagesPerSection.Length - 1;
        for (var i = 0; i < _pagesPerSection.Length; i++)
        {
            if (zeroBased < _sectionStarts[i] + _pagesPerSection[i])
            {
                sectionIndex = i;
                break;
            }
        }

        var sectionPage = zeroBased - _sectionStarts[sectionIndex];
        var pages = _pagesPerSection[sectionIndex];
        var fraction = pages <= 1 ? 0d : sectionPage / (double)pages;
        return new FlowContentLocator(sectionIndex, Math.Clamp(fraction, 0, 1));
    }

    public FlowContentLocator LocateProgress(double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        var page = progress >= 1 ? TotalPages : (int)Math.Floor(progress * TotalPages) + 1;
        return LocatePage(page);
    }
}
