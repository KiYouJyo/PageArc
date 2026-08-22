using System.Text.RegularExpressions;

namespace PageArc.Services;

public static partial class ReleaseNotesPresentation
{
    public static string ForLanguage(string? markdown, string language)
    {
        var locale = language.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN"
            : language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? "ja-JP"
            : "en-US";
        if (string.IsNullOrWhiteSpace(markdown)) return Unavailable(locale);

        var section = ExtractLocaleSection(markdown, locale);
        if (!string.IsNullOrWhiteSpace(section)) return NormalizeMarkdown(section);

        // GitHub's unstructured body is the English source. Never leak it into
        // Chinese or Japanese UI; localized releases can opt into explicit
        // `## zh-CN`, `## ja-JP`, and `## en-US` sections.
        return locale == "en-US" ? NormalizeMarkdown(markdown) : Unavailable(locale);
    }

    private static string? ExtractLocaleSection(string markdown, string locale)
    {
        var pattern = $@"(?ims)^##\s*{Regex.Escape(locale)}\s*$\s*(.*?)(?=^##\s*(?:zh-CN|ja-JP|en-US)\s*$|\z)";
        return Regex.Match(markdown, pattern).Groups.Cast<Group>().Skip(1).FirstOrDefault()?.Value.Trim();
    }

    private static string NormalizeMarkdown(string value) => MarkdownSyntax().Replace(value, string.Empty).Trim();

    private static string Unavailable(string locale) => locale switch
    {
        "zh-CN" => "此版本未提供简体中文发行说明。",
        "ja-JP" => "このバージョンには日本語のリリースノートがありません。",
        _ => "No release notes are available for this version."
    };

    [GeneratedRegex(@"(?m)^#{1,6}\s*|\*\*|__|`")]
    private static partial Regex MarkdownSyntax();
}
