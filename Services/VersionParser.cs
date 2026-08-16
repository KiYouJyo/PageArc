namespace PageArc.Services;

public static class VersionParser
{
    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;

        var value = tag.Trim();
        if (value.StartsWith('v') || value.StartsWith('V')) value = value[1..];
        var prerelease = value.IndexOfAny(['-', '+']);
        if (prerelease >= 0) value = value[..prerelease];

        if (!Version.TryParse(value, out var parsed) || parsed is null) return false;
        version = Normalize(parsed);
        return true;
    }

    public static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
}
