namespace PageArc.Models;

public enum LibraryImportDisposition
{
    Added,
    ExistingPath,
    DuplicateContent,
    Unsupported,
    Missing,
    Failed
}

public sealed record LibraryImportItemResult(
    string FilePath,
    LibraryImportDisposition Disposition,
    BookEntry? Book = null,
    string? ErrorMessage = null);

public sealed record LibraryImportProgress(int Completed, int Total, string CurrentPath);

public sealed class LibraryImportSummary
{
    public LibraryImportSummary(IReadOnlyList<LibraryImportItemResult> items)
    {
        Items = items;
    }

    public IReadOnlyList<LibraryImportItemResult> Items { get; }
    public int Added => Items.Count(x => x.Disposition == LibraryImportDisposition.Added);
    public int Existing => Items.Count(x => x.Disposition is LibraryImportDisposition.ExistingPath or LibraryImportDisposition.DuplicateContent);
    public int Unsupported => Items.Count(x => x.Disposition == LibraryImportDisposition.Unsupported);
    public int Failed => Items.Count(x => x.Disposition is LibraryImportDisposition.Missing or LibraryImportDisposition.Failed);
    public int Total => Items.Count;
}
