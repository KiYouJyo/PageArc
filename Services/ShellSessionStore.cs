using System.Text.Json;
using PageArc.Models;

namespace PageArc.Services;

public sealed class ShellSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;

    public ShellSessionStore(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath) ? AppPaths.ShellSessionFile : Path.GetFullPath(filePath);
    }

    public ShellSessionState Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new ShellSessionState();
            var state = JsonSerializer.Deserialize<ShellSessionState>(File.ReadAllText(_filePath), JsonOptions)
                        ?? new ShellSessionState();
            if (state.SchemaVersion != 1) return new ShellSessionState();
            state.Tabs ??= [];
            state.Tabs = Normalize(state.Tabs);
            if (!state.Tabs.Any(tab => string.Equals(tab.Id, state.SelectedTabId, StringComparison.Ordinal)))
                state.SelectedTabId = state.Tabs.FirstOrDefault()?.Id;
            return state;
        }
        catch
        {
            return new ShellSessionState();
        }
    }

    public void Save(ShellSessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var normalized = new ShellSessionState
        {
            SchemaVersion = 1,
            SelectedTabId = state.SelectedTabId,
            Tabs = Normalize(state.Tabs ?? [])
        };
        if (!normalized.Tabs.Any(tab => string.Equals(tab.Id, normalized.SelectedTabId, StringComparison.Ordinal)))
            normalized.SelectedTabId = normalized.Tabs.FirstOrDefault()?.Id;

        var directory = Path.GetDirectoryName(Path.GetFullPath(_filePath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temp = _filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(normalized, JsonOptions));
        File.Move(temp, _filePath, true);
    }

    private static List<ShellTabSession> Normalize(IEnumerable<ShellTabSession> tabs)
    {
        var result = new List<ShellTabSession>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tab in tabs)
        {
            if (tab is null || string.IsNullOrWhiteSpace(tab.Id) || !ids.Add(tab.Id)) continue;
            if (tab.Kind == ShellTabKind.Reader && string.IsNullOrWhiteSpace(tab.BookId)) continue;
            result.Add(tab);
        }
        return result;
    }
}
