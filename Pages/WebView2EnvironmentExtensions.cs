using Microsoft.Web.WebView2.Core;
using Windows.Storage.Streams;

namespace PageArc.Pages;

internal static class WebView2EnvironmentExtensions
{
    public static CoreWebView2WebResourceResponse CreateWebResourceResponse(
        this CoreWebView2Environment environment,
        Stream content,
        int statusCode,
        string reasonPhrase,
        string headers)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(content);

        var body = new InMemoryRandomAccessStream();
        return environment.CreateWebResourceResponse(body, statusCode, reasonPhrase, headers);
    }
}
