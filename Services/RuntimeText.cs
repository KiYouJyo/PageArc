namespace PageArc.Services;

public static class RuntimeText
{
    public static string ForLanguage(string? language, string zhCn, string jaJp, string enUs)
    {
        var value = language ?? string.Empty;
        if (value.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return zhCn;
        if (value.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return jaJp;
        return enUs;
    }

    public static string Current(string zhCn, string jaJp, string enUs) =>
        ForLanguage(App.Localization.CurrentLanguage, zhCn, jaJp, enUs);
}
