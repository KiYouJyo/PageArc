using PageArc.Models;

namespace PageArc.Services;

public static class BookFormatRegistry
{
    private static readonly EbookFormatDescriptor Epub = new("EPUB", ".epub", [".epub"]);
    private static readonly EbookFormatDescriptor Fb2 = new("FB2", ".fb2", [".fb2"]);
    private static readonly EbookFormatDescriptor Mobi = new("MOBI", ".mobi", [".mobi", ".azw"]);
    private static readonly EbookFormatDescriptor Azw3 = new("AZW3", ".azw3", [".azw3"]);
    private static readonly EbookFormatDescriptor Lit = new("LIT", ".lit", [".lit"]);

    private static readonly EbookFormatDescriptor[] Formats = [Epub, Fb2, Mobi, Azw3, Lit];
    private static readonly Dictionary<string, EbookFormatDescriptor> ById = Formats
        .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, EbookFormatDescriptor> ByExtension = Formats
        .SelectMany(format => format.Extensions.Select(extension => (Extension: extension, Format: format)))
        .ToDictionary(x => x.Extension, x => x.Format, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<EbookFormatDescriptor> RequiredFormats => Formats;

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var token = value.Trim();
        if (token.StartsWith('.'))
        {
            return ByExtension.TryGetValue(token, out var byExtension)
                ? byExtension.Id
                : token.TrimStart('.').ToUpperInvariant();
        }

        token = token.ToUpperInvariant();
        return token switch
        {
            "AZW" => "MOBI",
            "KF8" => "AZW3",
            _ => token
        };
    }

    public static bool TryGet(string? value, out EbookFormatDescriptor format) =>
        ById.TryGetValue(Normalize(value), out format!);

    public static EbookFormatDescriptor GetRequired(string value) =>
        TryGet(value, out var format)
            ? format
            : throw new NotSupportedException($"Unsupported ebook format: {value}");

    public static bool TryFromPath(string filePath, out EbookFormatDescriptor format)
    {
        format = null!;
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        return ByExtension.TryGetValue(Path.GetExtension(filePath), out format!);
    }

    public static string FormatFromPath(string filePath) =>
        TryFromPath(filePath, out var format) ? format.Id : string.Empty;

    public static bool IsSupportedPath(string filePath) => TryFromPath(filePath, out _);

    public static bool IsRequired(string? value) => TryGet(value, out _);
}
