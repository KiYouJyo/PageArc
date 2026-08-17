using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class EbookOpenErrorFormatterTests
{
    [Theory]
    [InlineData("zh-CN")]
    [InlineData("ja-JP")]
    [InlineData("en-US")]
    public void CalibreTraceback_IsReplacedWithFriendlyMessage(string language)
    {
        var error = new FileNotFoundException(
            "Traceback (most recent call last): FileNotFoundError: No such file or directory: C:/PageArc/NormalizedBooks/book/azw3/source.epub");

        var message = EbookOpenErrorFormatter.Format(error, language);

        Assert.DoesNotContain("Traceback", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source.epub", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FileNotFoundError", message, StringComparison.OrdinalIgnoreCase);
    }
}
