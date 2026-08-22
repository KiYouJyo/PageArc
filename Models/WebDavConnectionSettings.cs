namespace PageArc.Models;

public sealed record WebDavConnectionSettings(string Endpoint, string Username)
{
    public Uri GetEndpointUri()
    {
        if (!Uri.TryCreate(Endpoint?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("A valid HTTP or HTTPS WebDAV file URL is required.", nameof(Endpoint));
        return uri;
    }
}
