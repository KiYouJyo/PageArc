namespace PageArc.Services;

public readonly record struct ReaderSurfaceGeometry(double MaxWidth, double MaxHeight);

public static class ReaderLayoutPolicy
{
    public const double SpreadGap = 84d;

    public static int ResolveSpreadStartIndex(int requestedIndex, int sectionCount, string spreadMode)
    {
        if (sectionCount <= 0) return 0;

        var index = Math.Clamp(requestedIndex, 0, sectionCount - 1);
        return spreadMode switch
        {
            "odd" => index - (index % 2),
            "even" when index == 0 => 0,
            "even" => index % 2 == 0 ? index - 1 : index,
            _ => index
        };
    }

    public static bool HasLeadingBlankPage(int spreadStartIndex, string spreadMode) =>
        spreadStartIndex == 0 && string.Equals(spreadMode, "even", StringComparison.Ordinal);

    public static int ResolvePreviousSpreadStartIndex(int currentStartIndex, string spreadMode)
    {
        if (currentStartIndex <= 0) return 0;
        if (string.Equals(spreadMode, "even", StringComparison.Ordinal) && currentStartIndex == 1) return 0;
        return Math.Max(0, currentStartIndex - 2);
    }

    public static ReaderSurfaceGeometry ResolveSurfaceGeometry(
        string pageWidth,
        string zoomMode,
        bool isSpread,
        double availableWidth,
        double availableHeight)
    {
        var safeWidth = Math.Max(1d, availableWidth);
        var safeHeight = Math.Max(1d, availableHeight);
        _ = pageWidth;
        _ = zoomMode;
        _ = isSpread;
        return new ReaderSurfaceGeometry(safeWidth, safeHeight);
    }
}
