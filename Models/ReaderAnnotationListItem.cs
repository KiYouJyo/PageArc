using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace PageArc.Models;

public sealed class ReaderAnnotationListItem
{
    public required ReaderAnnotation Annotation { get; init; }

    public string ChapterTitle => Annotation.ChapterTitle;
    public string Quote => Annotation.Quote;
    public string Note => Annotation.Note ?? string.Empty;
    public Visibility NoteVisibility => string.IsNullOrWhiteSpace(Annotation.Note) ? Visibility.Collapsed : Visibility.Visible;

    public Brush AccentBrush => new SolidColorBrush(Annotation.HighlightColor.ToLowerInvariant() switch
    {
        "blue" => ColorHelper.FromArgb(255, 107, 184, 235),
        "green" => ColorHelper.FromArgb(255, 140, 199, 128),
        _ => ColorHelper.FromArgb(255, 250, 194, 46)
    });
}
