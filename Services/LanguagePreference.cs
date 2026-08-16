namespace PageArc.Services;

public static class LanguagePreference
{
    public const string SystemValue = "system";
    public static IReadOnlyList<string> SupportedBcp47Languages { get; } = ["zh-CN", "ja-JP", "en-US"];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, SystemValue, StringComparison.OrdinalIgnoreCase))
            return SystemValue;

        return SupportedBcp47Languages.FirstOrDefault(x =>
                   string.Equals(x, value.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? SystemValue;
    }

    public static string? ResolveOverride(string? storedValue)
    {
        var normalized = Normalize(storedValue);
        return normalized == SystemValue ? null : normalized;
    }

    public static string ResolveSystemLanguage(IReadOnlyList<string>? systemLanguages)
    {
        var first = systemLanguages?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "en-US";
        if (first.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        if (first.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja-JP";
        if (first.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "en-US";
        return "en-US";
    }

    public static string ResolveEffectiveLanguage(string? storedValue, IReadOnlyList<string>? systemLanguages) =>
        ResolveOverride(storedValue) ?? ResolveSystemLanguage(systemLanguages);
}
