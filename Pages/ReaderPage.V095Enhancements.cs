using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class ReaderPage
{
    private bool _v095EnhancementsWired;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        InitializeV095ReaderEnhancements();
    }

    private void InitializeV095ReaderEnhancements()
    {
        if (_v095EnhancementsWired) return;
        _ = InitializeV095ReaderEnhancementsAsync();
    }

    private async Task InitializeV095ReaderEnhancementsAsync()
    {
        if (_v095EnhancementsWired) return;
        try
        {
            await ReaderWebView.EnsureCoreWebView2Async();
            var core = ReaderWebView.CoreWebView2;
            if (core is null) return;
            core.WebMessageReceived += ReaderV095_WebMessageReceived;
            ReaderWebView.NavigationCompleted += ReaderV095_NavigationCompleted;
            _v095EnhancementsWired = true;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("v0.9.5 reader enhancements could not initialize.", ex);
        }
    }

    private async void ReaderV095_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || ReaderWebView.CoreWebView2 is null) return;
        try { await ReaderWebView.CoreWebView2.ExecuteScriptAsync(ReaderEnhancementScript.Build(App.Localization.CurrentLanguage)); }
        catch (Exception ex) { StartupDiagnostics.Log("v0.9.5 reader enhancement script injection failed.", ex); }
    }

    private async void ReaderV095_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        string message;
        try { message = args.TryGetWebMessageAsString(); }
        catch { return; }
        if (string.IsNullOrWhiteSpace(message) || message[0] != '{') return;

        try
        {
            using var json = JsonDocument.Parse(message);
            if (!json.RootElement.TryGetProperty("type", out var type)
                || !string.Equals(type.GetString(), "pagearc-image-save", StringComparison.Ordinal)) return;
            var name = json.RootElement.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
            var dataUrl = json.RootElement.TryGetProperty("dataUrl", out var dataNode) ? dataNode.GetString() : null;
            var source = json.RootElement.TryGetProperty("source", out var sourceNode) ? sourceNode.GetString() : null;
            await SaveReaderImageAsync(name, dataUrl, source);
        }
        catch (Exception ex) { StartupDiagnostics.Log("Reader image save request failed.", ex); }
    }

    private async Task SaveReaderImageAsync(string? suggestedName, string? dataUrl, string? source)
    {
        byte[]? bytes = null;
        var extension = ".png";
        if (!string.IsNullOrWhiteSpace(dataUrl) && TryDecodeDataUrl(dataUrl, out var decoded, out var mime))
        {
            bytes = decoded;
            extension = ExtensionForMime(mime);
        }
        else if (!string.IsNullOrWhiteSpace(source) && TryResolveReaderResource(source, out var sourcePath) && File.Exists(sourcePath))
        {
            bytes = await File.ReadAllBytesAsync(sourcePath);
            extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension)) extension = ".png";
        }
        if (bytes is null || bytes.Length == 0) return;
        var fileStem = SanitizeFileName(string.IsNullOrWhiteSpace(suggestedName) ? "ebook-image" : suggestedName!);
        var output = await PickerService.PickImageSavePathAsync(fileStem, extension);
        if (string.IsNullOrWhiteSpace(output)) return;
        await File.WriteAllBytesAsync(output, bytes);
    }

    private bool TryResolveReaderResource(string source, out string path)
    {
        path = string.Empty;
        if (_document?.CacheRoot is not { Length: > 0 } cacheRoot
            || !Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Host, "pagearc.local", StringComparison.OrdinalIgnoreCase)) return false;
        var root = Path.GetFullPath(cacheRoot);
        var relative = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')).Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(root, relative));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) && !string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)) return false;
        path = candidate;
        return true;
    }

    private static bool TryDecodeDataUrl(string value, out byte[] bytes, out string mime)
    {
        bytes = [];
        mime = "image/png";
        if (!value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return false;
        var comma = value.IndexOf(',');
        if (comma <= 5) return false;
        var metadata = value[5..comma];
        if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase)) return false;
        mime = metadata.Split(';', 2)[0];
        try { bytes = Convert.FromBase64String(value[(comma + 1)..]); return true; }
        catch { return false; }
    }

    private static string ExtensionForMime(string? mime) => mime?.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg", "image/gif" => ".gif", "image/webp" => ".webp", "image/svg+xml" => ".svg", "image/bmp" => ".bmp", _ => ".png"
    };

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "ebook-image" : result;
    }
}
