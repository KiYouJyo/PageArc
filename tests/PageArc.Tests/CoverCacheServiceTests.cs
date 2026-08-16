using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class CoverCacheServiceTests
{
    [Fact]
    public async Task SaveDataUrl_WritesSupportedImagePayload()
    {
        var id = $"cover-{Guid.NewGuid():N}";
        var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        var dataUrl = "data:image/png;base64," + Convert.ToBase64String(bytes);
        try
        {
            var path = await CoverCacheService.SaveDataUrlAsync(id, dataUrl);
            Assert.NotNull(path);
            Assert.EndsWith(".png", path, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path!));
        }
        finally
        {
            if (Directory.Exists(AppPaths.CoversRoot))
                foreach (var path in Directory.EnumerateFiles(AppPaths.CoversRoot, id + ".*"))
                    File.Delete(path);
        }
    }

    [Theory]
    [InlineData("https://example.com/cover.png")]
    [InlineData("data:text/plain;base64,SGVsbG8=")]
    [InlineData("data:image/png,not-base64")]
    [InlineData("data:image/png;base64,***")]
    public async Task SaveDataUrl_RejectsUnsupportedOrInvalidPayload(string value)
    {
        Assert.Null(await CoverCacheService.SaveDataUrlAsync(Guid.NewGuid().ToString("N"), value));
    }
}
