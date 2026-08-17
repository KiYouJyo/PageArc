using System.Text.Json;
using PageArc.Models;

namespace PageArc.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _settingsFile;

    public SettingsService(string? settingsFile = null)
    {
        _settingsFile = string.IsNullOrWhiteSpace(settingsFile) ? AppPaths.SettingsFile : Path.GetFullPath(settingsFile);
    }

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        EnsureStorage();
        try
        {
            if (File.Exists(_settingsFile))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsFile)) ?? new();
        }
        catch
        {
            Current = new();
        }

        Current.Language = LanguagePreference.Normalize(Current.Language);
    }

    public void Update(Action<AppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        lock (_gate)
        {
            update(Current);
            SaveUnsafe();
        }
    }

    private void SaveUnsafe()
    {
        EnsureStorage();
        var temp = _settingsFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Current, JsonOptions));
        File.Move(temp, _settingsFile, true);
    }

    private void EnsureStorage()
    {
        if (string.Equals(_settingsFile, AppPaths.SettingsFile, StringComparison.OrdinalIgnoreCase))
            AppPaths.Ensure();
        var directory = Path.GetDirectoryName(_settingsFile);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
    }
}
