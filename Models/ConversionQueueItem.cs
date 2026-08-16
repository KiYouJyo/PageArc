namespace PageArc.Models;

public sealed class ConversionQueueItem
{
    public required string FilePath { get; init; }
    public string FileName => Path.GetFileName(FilePath);
    public string Format => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();
    public string Status { get; set; } = "Ready";
}
