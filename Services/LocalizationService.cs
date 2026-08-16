using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Globalization;
using PageArc.Models;
using Windows.System.UserProfile;

namespace PageArc.Services;

public sealed class LocalizationService
{
    private readonly SettingsService _settings;
    private readonly object _gate = new();
    private ResourceLoader? _loader;

    public LocalizationService(SettingsService settings)
    {
        _settings = settings;
    }

    public string CurrentLanguage { get; private set; } = "en-US";

    public event EventHandler? LanguageChanged;

    public void ApplyPersistedLanguage(AppSettings settings)
    {
        var effective = LanguagePreference.ResolveEffectiveLanguage(settings.Language, GlobalizationPreferences.Languages);
        ApplicationLanguages.PrimaryLanguageOverride = effective;
        var culture = CultureInfo.GetCultureInfo(effective);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        lock (_gate)
        {
            _loader = new ResourceLoader();
            CurrentLanguage = effective;
        }
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        try
        {
            ResourceLoader loader;
            lock (_gate)
            {
                _loader ??= new ResourceLoader();
                loader = _loader;
            }
            var value = loader.GetString(key.Replace('.', '/'));
            return string.IsNullOrWhiteSpace(value) ? $"!{key}!" : value;
        }
        catch
        {
            return $"!{key}!";
        }
    }

    public bool SwitchLanguage(string requestedLanguage)
    {
        var normalized = LanguagePreference.Normalize(requestedLanguage);
        var effective = LanguagePreference.ResolveEffectiveLanguage(normalized, GlobalizationPreferences.Languages);
        if (string.Equals(effective, CurrentLanguage, StringComparison.OrdinalIgnoreCase))
        {
            _settings.Update(x => x.Language = normalized);
            return true;
        }

        var previousOverride = ApplicationLanguages.PrimaryLanguageOverride;
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var previousLanguage = CurrentLanguage;
        try
        {
            ApplicationLanguages.PrimaryLanguageOverride = effective;
            var culture = CultureInfo.GetCultureInfo(effective);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            var replacementLoader = new ResourceLoader();
            _settings.Update(x => x.Language = normalized);
            lock (_gate)
            {
                _loader = replacementLoader;
                CurrentLanguage = effective;
            }
            LanguageChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            ApplicationLanguages.PrimaryLanguageOverride = previousOverride;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
            CurrentLanguage = previousLanguage;
            return false;
        }
    }
}
