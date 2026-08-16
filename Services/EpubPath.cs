namespace PageArc.Services;

public static class EpubPath
{
    public static string Normalize(string path)
    {
        var clean = DecodeAndStrip(path);
        var stack = new List<string>();
        foreach (var segment in clean.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                continue;
            }
            stack.Add(segment);
        }
        return string.Join('/', stack);
    }

    public static string Combine(string directory, string href)
    {
        var cleanDirectory = Normalize(directory);
        var cleanHref = DecodeAndStrip(href);
        return string.IsNullOrWhiteSpace(cleanDirectory)
            ? Normalize(cleanHref)
            : Normalize($"{cleanDirectory}/{cleanHref}");
    }

    public static string ToWebPath(string relativePath) =>
        string.Join('/', Normalize(relativePath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));

    private static string DecodeAndStrip(string path)
    {
        var clean = (path ?? string.Empty).Replace('\\', '/');
        var fragment = clean.IndexOf('#');
        if (fragment >= 0) clean = clean[..fragment];
        var query = clean.IndexOf('?');
        if (query >= 0) clean = clean[..query];
        try
        {
            clean = Uri.UnescapeDataString(clean);
        }
        catch (UriFormatException)
        {
            // Keep the original path if malformed percent-encoding is encountered.
        }
        return clean.Replace('\\', '/');
    }
}
