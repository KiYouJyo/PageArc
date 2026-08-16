using System.Text;
using PageArc.Models;

namespace PageArc.Services;

public static class AppActivationRequestParser
{
    public const string ProtocolScheme = "pagearc";

    public static AppActivationRequest FromFilePaths(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var supported = paths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(SafeFullPath)
            .Where(BookFormatRegistry.IsSupportedPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return supported.Length == 0 ? AppActivationRequest.Launch() : AppActivationRequest.Files(supported);
    }

    public static AppActivationRequest FromProtocol(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!string.Equals(uri.Scheme, ProtocolScheme, StringComparison.OrdinalIgnoreCase))
            return AppActivationRequest.Protocol(uri);

        var host = uri.Host;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (string.Equals(host, "book", StringComparison.OrdinalIgnoreCase) && segments.Length >= 1)
            return AppActivationRequest.Book(Uri.UnescapeDataString(segments[0]), uri);

        if (string.Equals(host, "open", StringComparison.OrdinalIgnoreCase))
        {
            var query = ParseQuery(uri.Query);
            if (query.TryGetValue("book", out var bookId) && !string.IsNullOrWhiteSpace(bookId))
                return AppActivationRequest.Book(bookId, uri);
            if (query.TryGetValue("path", out var path) && BookFormatRegistry.IsSupportedPath(path))
                return AppActivationRequest.Files([SafeFullPath(path)]);
        }

        return AppActivationRequest.Protocol(uri);
    }

    public static AppActivationRequest FromLaunchArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return AppActivationRequest.Launch(arguments);
        var trimmed = arguments.Trim();
        if (Uri.TryCreate(trimmed.Trim('"'), UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, ProtocolScheme, StringComparison.OrdinalIgnoreCase))
            return FromProtocol(uri);

        var paths = SplitCommandLine(trimmed)
            .Select(Unquote)
            .Where(BookFormatRegistry.IsSupportedPath)
            .Select(SafeFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return paths.Length == 0 ? AppActivationRequest.Launch(arguments) : AppActivationRequest.Files(paths, arguments);
    }

    public static Uri CreateBookUri(string bookId)
    {
        if (string.IsNullOrWhiteSpace(bookId)) throw new ArgumentException("Book id is required.", nameof(bookId));
        return new Uri($"{ProtocolScheme}://book/{Uri.EscapeDataString(bookId)}");
    }

    internal static IReadOnlyList<string> SplitCommandLine(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return [];
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var backslashes = 0;

        foreach (var ch in commandLine)
        {
            if (ch == '\\')
            {
                backslashes++;
                continue;
            }

            if (ch == '"')
            {
                if (backslashes > 0)
                {
                    current.Append('\\', backslashes / 2);
                    if (backslashes % 2 == 1) current.Append('"');
                    else inQuotes = !inQuotes;
                    backslashes = 0;
                    continue;
                }
                inQuotes = !inQuotes;
                continue;
            }

            if (backslashes > 0)
            {
                current.Append('\\', backslashes);
                backslashes = 0;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(ch);
        }

        if (backslashes > 0) current.Append('\\', backslashes);
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query)) return result;
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            var key = Uri.UnescapeDataString(pieces[0].Replace('+', ' '));
            var value = pieces.Length > 1 ? Uri.UnescapeDataString(pieces[1].Replace('+', ' ')) : string.Empty;
            result[key] = value;
        }
        return result;
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }
}
