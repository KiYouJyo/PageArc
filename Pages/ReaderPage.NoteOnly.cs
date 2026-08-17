namespace PageArc.Pages;

public sealed partial class ReaderPage
{
    private void FilterAnnotationItemsToNotes()
    {
        var notes = _annotationItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Annotation.Note))
            .ToArray();
        if (notes.Length != _annotationItems.Count)
        {
            _annotationItems.Clear();
            foreach (var item in notes) _annotationItems.Add(item);
        }

        var language = App.Localization.CurrentLanguage;
        if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            NotesHeading.Text = "笔记";
            NotesMetaText.Text = $"{notes.Length} 条笔记";
            NotesFooterText.Text = $"{notes.Length} 条笔记";
        }
        else if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            NotesHeading.Text = "ノート";
            NotesMetaText.Text = $"ノート {notes.Length} 件";
            NotesFooterText.Text = $"{notes.Length} 件";
        }
        else
        {
            NotesHeading.Text = "Notes";
            NotesMetaText.Text = $"{notes.Length} notes";
            NotesFooterText.Text = $"{notes.Length} notes";
        }
    }
}
