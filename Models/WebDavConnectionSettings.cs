namespace PageArc.Models;

public sealed record WebDavConnectionSettings(string Endpoint, string Username)
{
    public const string DefaultArchiveFileName = "PageArc-library.pagearcbackup";

    public Uri GetEndpointUri()
    {
        var configured = GetConfiguredUri();
        if (LooksLikeFileUrl(configured)) return configured;

        var collection = EnsureTrailingSlash(configured);
        return new Uri(collection, Uri.EscapeDataString(DefaultArchiveFileName));
    }

    public bool UsesDirectArchiveUrl => LooksLikeFileUrl(GetConfiguredUri());

    public string? GetDirectArchiveFileName()
    {
        var configured = GetConfiguredUri();
        return LooksLikeFileUrl(configured) ? Uri.UnescapeDataString(Path.GetFileName(configured.AbsolutePath)) : null;
    }

    public Uri GetArchiveUri(string fileName)
    {
        if (!WebDavArchiveItem.IsPageArcBackupFileName(fileName))
            throw new ArgumentException("A valid PageArc .pagearcbackup filename is required.", nameof(fileName));
        return new Uri(GetCollectionUri(), Uri.EscapeDataString(fileName));
    }

    public Uri GetUploadUri(string fileName)
    {
        var configured = GetConfiguredUri();
        return LooksLikeFileUrl(configured) ? configured : GetArchiveUri(fileName);
    }

    public Uri GetCollectionUri()
    {
        var configured = GetConfiguredUri();
        if (!LooksLikeFileUrl(configured)) return EnsureTrailingSlash(configured);

        var builder = new UriBuilder(configured);
        var path = builder.Path;
        var slash = path.LastIndexOf('/');
        builder.Path = slash >= 0 ? path[..(slash + 1)] : "/";
        builder.Query = string.Empty;
        builder.Fragment = string.Empty;
        return builder.Uri;
    }

    private Uri GetConfiguredUri()
    {
        if (!Uri.TryCreate(Endpoint?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("A valid HTTP or HTTPS WebDAV URL is required.", nameof(Endpoint));
        return uri;
    }

    private static bool LooksLikeFileUrl(Uri uri)
    {
        var path = uri.AbsolutePath;
        return path.EndsWith(".pagearcbackup", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        if (uri.AbsolutePath.EndsWith('/')) return uri;
        var builder = new UriBuilder(uri) { Path = uri.AbsolutePath + "/" };
        return builder.Uri;
    }
}
