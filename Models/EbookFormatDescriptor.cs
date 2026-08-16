namespace PageArc.Models;

public sealed record EbookFormatDescriptor(
    string Id,
    string PrimaryExtension,
    IReadOnlyList<string> Extensions);
