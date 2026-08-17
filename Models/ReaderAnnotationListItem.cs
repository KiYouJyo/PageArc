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

    // v0.9.3 deliberately uses one low-saturation red marker for note-bearing text.
    // HighlightColor remains in persisted data for backward compatibility and a later custom-color feature.
    public Brush AccentBrush => new SolidColorBrush(ColorHelper.FromArgb(255, 185, 111, 111));
}
