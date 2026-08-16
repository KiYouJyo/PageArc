using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.Globalization;
using PageArc.Models;
using Windows.System.UserProfile;

namespace PageArc.Services;

public sealed class LocalizationService
{
    private readonly SettingsService _settings;
    private ResourceLoader? _loader;

    public LocalizationService(SettingsService settings)
    {
        _settings = settings;
    }

    public string CurrentLanguage { get; private set; } = "en-US";

    public void ApplyPersistedLanguage(AppSettings settings)
    {
        var effective = LanguagePreference.ResolveEffectiveLanguage(settings.Language, GlobalizationPreferences.Languages);
        ApplicationLanguages.PrimaryLanguageOverride = effective;
        var culture = CultureInfo.GetCultureInfo(effective);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        _loader = new ResourceLoader();
        CurrentLanguage = effective;
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        try
        {
            _loader ??= new ResourceLoader();
            var value = _loader.GetString(key.Replace('.', '/'));
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
        _settings.Update(x => x.Language = normalized);
        ApplicationLanguages.PrimaryLanguageOverride = effective;
        var culture = CultureInfo.GetCultureInfo(effective);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        _loader = new ResourceLoader();
        CurrentLanguage = effective;
        return true;
    }
}
