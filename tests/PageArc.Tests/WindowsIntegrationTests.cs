using System.Xml.Linq;
using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class WindowsIntegrationTests
{
    [Theory]
    [InlineData(@"C:\Books\Example.epub", "EPUB")]
    [InlineData(@"C:\Books\Example.fb2", "FB2")]
    [InlineData(@"C:\Books\Example.mobi", "MOBI")]
    [InlineData(@"C:\Books\Example.azw", "MOBI")]
    [InlineData(@"C:\Books\Example.azw3", "AZW3")]
    [InlineData(@"C:\Books\Example.lit", "LIT")]
    public void LaunchArguments_ParseAllAssociatedEbookExtensions(string path, string expectedFormat)
    {
        var request = AppActivationRequestParser.FromLaunchArguments($"\"{path}\"");
        Assert.Equal(AppActivationRequestKind.Files, request.Kind);
        var parsed = Assert.Single(request.FilePaths);
        Assert.Equal(expectedFormat, BookFormatRegistry.FormatFromPath(parsed));
    }

    [Fact]
    public void LaunchArguments_PreserveQuotedPathsWithSpaces()
    {
        const string path = @"C:\My Books\A Great Book.epub";
        var request = AppActivationRequestParser.FromLaunchArguments($"\"{path}\"");
        Assert.Equal(AppActivationRequestKind.Files, request.Kind);
        Assert.EndsWith(@"My Books\A Great Book.epub", Assert.Single(request.FilePaths), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BookProtocol_RoundTripsStableLibraryId()
    {
        const string id = "book id/with symbols";
        var uri = AppActivationRequestParser.CreateBookUri(id);
        var request = AppActivationRequestParser.FromProtocol(uri);
        Assert.Equal(AppActivationRequestKind.Book, request.Kind);
        Assert.Equal(id, request.BookId);
    }

    [Fact]
    public void OpenProtocol_ParsesEncodedBookId()
    {
        var request = AppActivationRequestParser.FromProtocol(new Uri("pagearc://open?book=abc%20123"));
        Assert.Equal(AppActivationRequestKind.Book, request.Kind);
        Assert.Equal("abc 123", request.BookId);
    }

    [Fact]
    public void PackageManifest_DeclaresAllRequiredAssociationsAndProtocol()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Packaging", "PageArc.Package.appxmanifest");
        Assert.True(File.Exists(path), path);
        var document = XDocument.Load(path);
        var fileTypes = document.Descendants()
            .Where(x => x.Name.LocalName == "FileType")
            .Select(x => x.Value.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var extension in new[] { ".epub", ".fb2", ".mobi", ".azw", ".azw3", ".lit" })
            Assert.Contains(extension, fileTypes);

        var protocols = document.Descendants()
            .Where(x => x.Name.LocalName == "Protocol")
            .Select(x => (string?)x.Attribute("Name"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        Assert.Contains(protocols, x => string.Equals(x, "pagearc", StringComparison.OrdinalIgnoreCase));
    }
}
