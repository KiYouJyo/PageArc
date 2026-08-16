using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using PageArc.Models;

namespace PageArc.Services;

public sealed class WebViewKindleParserRuntime : IKindleParserRuntime
{
    private const string RuntimeHost = "pagearc-kindle.local";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly WebView2 _webView;
    private string? _workspace;
    private bool _initialized;
    private bool _bookOpen;
    private bool _disposed;

    public WebViewKindleParserRuntime(WebView2 webView)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
    }

    public async Task<KindleRuntimeBook> OpenAsync(BookEntry book, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(book);
        if (!File.Exists(book.FilePath)) throw new FileNotFoundException("Kindle source file not found.", book.FilePath);

        var format = BookFormatRegistry.Normalize(book.Format);
        if (string.IsNullOrWhiteSpace(format)) format = BookFormatRegistry.FormatFromPath(book.FilePath);
        if (format is not ("MOBI" or "AZW3"))
            throw new NotSupportedException($"The built-in Kindle parser cannot open {format}.");

        await EnsureInitializedAsync(cancellationToken);
        await CloseAsync(cancellationToken);
        _workspace = PrepareWorkspace(book);

        var core = _webView.CoreWebView2 ?? throw new InvalidOperationException("Kindle parser WebView2 is unavailable.");
        core.SetVirtualHostNameToFolderMapping(RuntimeHost, _workspace, CoreWebView2HostResourceAccessKind.Allow);
        await NavigateAsync($"https://{RuntimeHost}/pagearc-host.html", cancellationToken);

        var fileName = Path.GetFileName(GetWorkspaceSourcePath(book));
        var expression = $"window.pageArcKindle.open({JsonSerializer.Serialize(fileName)}, {JsonSerializer.Serialize(format)})";
        var parsed = await InvokeAsync<KindleRuntimeBook>(expression, cancellationToken);
        parsed.Format = format;
        if (string.IsNullOrWhiteSpace(parsed.Title)) parsed.Title = book.Title;
        if (string.IsNullOrWhiteSpace(parsed.Author)) parsed.Author = book.Author;
        _bookOpen = true;
        return parsed;
    }

    public async Task<KindleRuntimeSectionContent> LoadSectionAsync(int flowSectionIndex, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_bookOpen) throw new InvalidOperationException("No Kindle book is open in the parser runtime.");
        if (flowSectionIndex < 0) throw new ArgumentOutOfRangeException(nameof(flowSectionIndex));

        return await InvokeAsync<KindleRuntimeSectionContent>(
            $"window.pageArcKindle.loadSection({flowSectionIndex})",
            cancellationToken);
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !_initialized) return;
        if (_bookOpen)
        {
            try
            {
                await InvokeAsync<JsonElement>("window.pageArcKindle.close()", cancellationToken);
            }
            catch
            {
                // Navigation/disposal can tear down the parser document before the cleanup call completes.
            }
        }
        _bookOpen = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try { await CloseAsync(); }
        catch { }
        _disposed = true;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        cancellationToken.ThrowIfCancellationRequested();
        await _webView.EnsureCoreWebView2Async();
        var core = _webView.CoreWebView2 ?? throw new InvalidOperationException("Kindle parser WebView2 could not be initialized.");
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsWebMessageEnabled = false;
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, args) =>
        {
            if (!Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var uri)) return;
            if (uri.Scheme is not ("http" or "https")) return;
            if (string.Equals(uri.Host, RuntimeHost, StringComparison.OrdinalIgnoreCase)) return;
            args.Response = core.Environment.CreateWebResourceResponse(
                new Windows.Storage.Streams.InMemoryRandomAccessStream(),
                403,
                "Blocked",
                "Content-Type: text/plain");
        };
        _initialized = true;
    }

    private string PrepareWorkspace(BookEntry book)
    {
        AppPaths.Ensure();
        var workspace = Path.Combine(AppPaths.KindleParserRoot, book.Id);
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(Path.Combine(workspace, "vendor"));

        var runtimeRoot = Path.Combine(AppContext.BaseDirectory, "ThirdParty", "foliate-js");
        CopyRuntimeFile(Path.Combine(runtimeRoot, "mobi.js"), Path.Combine(workspace, "mobi.js"));
        CopyRuntimeFile(Path.Combine(runtimeRoot, "pagearc-host.html"), Path.Combine(workspace, "pagearc-host.html"));
        CopyRuntimeFile(Path.Combine(runtimeRoot, "vendor", "fflate.js"), Path.Combine(workspace, "vendor", "fflate.js"));
        File.Copy(book.FilePath, GetWorkspaceSourcePath(book, workspace), overwrite: true);
        return workspace;
    }

    private static void CopyRuntimeFile(string source, string destination)
    {
        if (!File.Exists(source))
            throw new FileNotFoundException("The packaged Kindle parser runtime is incomplete.", source);
        File.Copy(source, destination, overwrite: true);
    }

    private static string GetWorkspaceSourcePath(BookEntry book, string? workspace = null)
    {
        workspace ??= Path.Combine(AppPaths.KindleParserRoot, book.Id);
        var extension = Path.GetExtension(book.FilePath);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".mobi";
        return Path.Combine(workspace, "source" + extension.ToLowerInvariant());
    }

    private async Task NavigateAsync(string uri, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            _webView.NavigationCompleted -= NavigationCompleted;
            if (args.IsSuccess) completion.TrySetResult(true);
            else completion.TrySetException(new InvalidOperationException($"Kindle parser navigation failed: {args.WebErrorStatus}."));
        }

        _webView.NavigationCompleted += NavigationCompleted;
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        _webView.CoreWebView2.Navigate(uri);
        try
        {
            await completion.Task;
        }
        finally
        {
            _webView.NavigationCompleted -= NavigationCompleted;
        }
    }

    private async Task<T> InvokeAsync<T>(string expression, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var script = """
            (async () => {
              try {
                const value = __EXPRESSION__;
                return JSON.stringify({ ok: true, value: await value });
              } catch (error) {
                return JSON.stringify({ ok: false, error: String(error?.message ?? error) });
              }
            })()
            """.Replace("__EXPRESSION__", expression, StringComparison.Ordinal);

        var raw = await _webView.CoreWebView2.ExecuteScriptAsync(script);
        cancellationToken.ThrowIfCancellationRequested();
        var envelopeJson = DecodeWebViewString(raw);
        var envelope = JsonSerializer.Deserialize<RuntimeEnvelope>(envelopeJson, JsonOptions)
            ?? throw new InvalidDataException("Kindle parser returned an empty response.");
        if (!envelope.Ok)
        {
            var message = envelope.Error ?? "Kindle parser failed.";
            if (message.Contains("PAGEARC_DRM", StringComparison.Ordinal))
                throw new DrmProtectedEbookException("This Kindle ebook is encrypted or DRM-protected and cannot be opened by PageArc.");
            throw new InvalidDataException(message);
        }
        if (typeof(T) == typeof(JsonElement) && envelope.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return (T)(object)default(JsonElement);
        return envelope.Value.Deserialize<T>(JsonOptions)
            ?? throw new InvalidDataException("Kindle parser returned an invalid payload.");
    }

    private static string DecodeWebViewString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new InvalidDataException("Kindle parser returned no data.");
        try
        {
            return JsonSerializer.Deserialize<string>(raw) ?? raw;
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    private sealed class RuntimeEnvelope
    {
        public bool Ok { get; set; }
        public JsonElement Value { get; set; }
        public string? Error { get; set; }
    }
}
