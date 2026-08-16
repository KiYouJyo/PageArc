using System.Text.Json;
using PageArc.Models;

namespace PageArc.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        AppPaths.Ensure();
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile)) ?? new();
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
        AppPaths.Ensure();
        var temp = AppPaths.SettingsFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Current, JsonOptions));
        File.Move(temp, AppPaths.SettingsFile, true);
    }
}
