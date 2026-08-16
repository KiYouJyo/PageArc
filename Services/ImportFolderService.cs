using System.Collections.ObjectModel;
using System.Text.Json;
using PageArc.Models;

namespace PageArc.Services;

public sealed class ImportFolderService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly LibraryService _library;
    private readonly string _stateFile;
    private readonly object _gate = new();

    public ImportFolderService(LibraryService library, string? stateFile = null)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _stateFile = string.IsNullOrWhiteSpace(stateFile) ? AppPaths.ImportFoldersFile : Path.GetFullPath(stateFile);
    }

    public ObservableCollection<ImportFolderEntry> Folders { get; } = [];

    public void Load()
    {
        EnsureStorage();
        try
        {
            if (!File.Exists(_stateFile)) return;
            var entries = JsonSerializer.Deserialize<List<ImportFolderEntry>>(File.ReadAllText(_stateFile), JsonOptions) ?? [];
            Folders.Clear();
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.FolderPath)) continue;
                entry.FolderPath = SafeFullPath(entry.FolderPath);
                entry.IsAvailable = Directory.Exists(entry.FolderPath);
                if (string.IsNullOrWhiteSpace(entry.DisplayName)) entry.DisplayName = ResolveDisplayName(entry.FolderPath);
                Folders.Add(entry);
            }
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Import-folder state load failed.", ex);
            Folders.Clear();
        }
    }

    public async Task<ImportFolderScanResult> AddAsync(
        string folderPath,
        IProgress<LibraryImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) throw new ArgumentException("Folder path is required.", nameof(folderPath));
        var fullPath = SafeFullPath(folderPath);
        if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException(fullPath);

        var existing = Folders.FirstOrDefault(x => PathsEqual(x.FolderPath, fullPath));
        if (existing is not null)
            return await RescanAsync(existing, progress, cancellationToken);

        var entry = new ImportFolderEntry
        {
            FolderPath = fullPath,
            DisplayName = ResolveDisplayName(fullPath),
            IsAvailable = true
        };
        Folders.Add(entry);
        Save();
        return await RescanAsync(entry, progress, cancellationToken);
    }

    public async Task<ImportFolderScanResult> RescanAsync(
        ImportFolderEntry folder,
        IProgress<LibraryImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        cancellationToken.ThrowIfCancellationRequested();
        folder.IsAvailable = Directory.Exists(folder.FolderPath);
        if (!folder.IsAvailable)
        {
            folder.BookCount = 0;
            folder.LastScannedAt = DateTimeOffset.Now;
            Save();
            return new ImportFolderScanResult(folder, new LibraryImportSummary([]), 0);
        }

        var supported = await Task.Run(() => EnumerateSupportedFiles(folder.FolderPath, cancellationToken), cancellationToken);
        var summary = await _library.ImportManyAsync(supported, progress, cancellationToken);
        folder.BookCount = supported.Count;
        folder.LastScannedAt = DateTimeOffset.Now;
        folder.IsAvailable = true;
        Save();
        return new ImportFolderScanResult(folder, summary, supported.Count);
    }

    public bool Remove(ImportFolderEntry folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var removed = Folders.Remove(folder);
        if (removed) Save();
        return removed;
    }

    public void Save()
    {
        lock (_gate)
        {
            EnsureStorage();
            var temp = _stateFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Folders.ToList(), JsonOptions));
            File.Move(temp, _stateFile, true);
        }
    }

    internal static IReadOnlyList<string> EnumerateSupportedFiles(string root, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(root)) return [];
        var results = new List<string>();
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(root));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (BookFormatRegistry.IsSupportedPath(file)) results.Add(file);
                }
                foreach (var directory in Directory.EnumerateDirectories(current))
                    pending.Push(directory);
            }
            catch (UnauthorizedAccessException)
            {
                // Skip inaccessible subtrees; the rest of the monitored folder remains usable.
            }
            catch (DirectoryNotFoundException)
            {
                // The subtree may have disappeared during a scan.
            }
            catch (IOException)
            {
                // Skip transient filesystem failures and continue with other subtrees.
            }
        }

        return results
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void EnsureStorage()
    {
        if (string.Equals(_stateFile, AppPaths.ImportFoldersFile, StringComparison.OrdinalIgnoreCase)) AppPaths.Ensure();
        var directory = Path.GetDirectoryName(_stateFile);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
    }

    private static string ResolveDisplayName(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? trimmed : name;
    }

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }

    private static bool PathsEqual(string left, string right)
    {
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
    }
}
