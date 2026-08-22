using PageArc.Models;

namespace PageArc.Services;

/// <summary>
/// Provides a stable logical-page map for reflowable books without pretending that EPUB-style
/// documents contain fixed printed pages. The page estimate is derived from each flow section's
/// source size and is used only for direct page navigation; the canonical saved position remains
/// section+fraction and its global progress keeps PageArc's existing equal-section semantics.
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

    public int TotalPages { get; private set; }

    public bool IsFrozen { get; private set; }

    public void FreezeMeasuredPages(IReadOnlyList<int> measuredPages)
    {
        ArgumentNullException.ThrowIfNull(measuredPages);
        if (measuredPages.Count != _pagesPerSection.Length)
            throw new ArgumentException("Measured page count must match the document section count.", nameof(measuredPages));

        for (var i = 0; i < _pagesPerSection.Length; i++)
            _pagesPerSection[i] = Math.Max(1, measuredPages[i]);
        RebuildStarts();
        IsFrozen = true;
    }

    public int GetSectionStartPage(int sectionIndex)
    {
        sectionIndex = Math.Clamp(sectionIndex, 0, _pagesPerSection.Length - 1);
        return _sectionStarts[sectionIndex] + 1;
    }

    /// <summary>
    /// Replaces source-size estimates with the page faces measured by the reader.
    /// A spread can render more than one spine item, so measured faces are distributed
    /// across that exact range while preserving at least one page per section.
    /// </summary>
    public void UpdateRenderedRange(int startSectionIndex, int endSectionIndex, int measuredPages)
    {
        if (IsFrozen) return;
        startSectionIndex = Math.Clamp(startSectionIndex, 0, _pagesPerSection.Length - 1);
        endSectionIndex = Math.Clamp(endSectionIndex, startSectionIndex, _pagesPerSection.Length - 1);
        var sectionCount = endSectionIndex - startSectionIndex + 1;
        measuredPages = Math.Max(sectionCount, measuredPages);

        var estimateTotal = 0;
        for (var i = startSectionIndex; i <= endSectionIndex; i++) estimateTotal += _pagesPerSection[i];

        var remainingPages = measuredPages;
        var remainingSections = sectionCount;
        for (var i = startSectionIndex; i <= endSectionIndex; i++)
        {
            var pages = i == endSectionIndex
                ? remainingPages
                : Math.Clamp(
                    (int)Math.Round(measuredPages * (_pagesPerSection[i] / (double)Math.Max(1, estimateTotal))),
                    1,
                    remainingPages - (remainingSections - 1));
            _pagesPerSection[i] = pages;
            remainingPages -= pages;
            remainingSections--;
        }

        RebuildStarts();
    }

    private void RebuildStarts()
    {
        var running = 0;
        for (var i = 0; i < _pagesPerSection.Length; i++)
        {
            _sectionStarts[i] = running;
            running += _pagesPerSection[i];
        }
        TotalPages = Math.Max(1, running);
    }

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
        if (progress >= 1) return new FlowContentLocator(_pagesPerSection.Length - 1, 1);
        var page = Math.Clamp((int)Math.Floor(progress * TotalPages) + 1, 1, TotalPages);
        return LocatePage(page);
    }
}
