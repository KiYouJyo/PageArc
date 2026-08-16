using System.Text.Json.Serialization;

namespace PageArc.Models;

public sealed class ImportFolderEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FolderPath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int BookCount { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastScannedAt { get; set; }
    public bool IsAvailable { get; set; } = true;

    [JsonIgnore]
    public string EffectiveName => string.IsNullOrWhiteSpace(DisplayName)
        ? Path.GetFileName(FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        : DisplayName;
}

public sealed record ImportFolderScanResult(
    ImportFolderEntry Folder,
    LibraryImportSummary ImportSummary,
    int SupportedFilesFound);
