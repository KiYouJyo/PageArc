using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class ReaderLayoutPolicyTests
{
    [Theory]
    [InlineData("odd", 0, 0)]
    [InlineData("odd", 1, 0)]
    [InlineData("odd", 2, 2)]
    [InlineData("even", 0, 0)]
    [InlineData("even", 1, 1)]
    [InlineData("even", 2, 1)]
    [InlineData("even", 120, 119)]
    public void SpreadStart_UsesDifferentOddAndEvenPairing(string mode, int requested, int expected)
    {
        Assert.Equal(expected, ReaderLayoutPolicy.ResolveSpreadStartIndex(requested, 200, mode));
    }

    [Fact]
    public void EvenSpread_UsesOneLeadingBlankThenPairsFollowingPages()
    {
        Assert.True(ReaderLayoutPolicy.HasLeadingBlankPage(0, "even"));
        Assert.False(ReaderLayoutPolicy.HasLeadingBlankPage(1, "even"));
        Assert.Equal(0, ReaderLayoutPolicy.ResolvePreviousSpreadStartIndex(1, "even"));
        Assert.Equal(1, ReaderLayoutPolicy.ResolvePreviousSpreadStartIndex(3, "even"));
    }

    [Fact]
    public void Geometry_AlwaysFillsTheActualReadingArea()
    {
        var fitWidth = ReaderLayoutPolicy.ResolveSurfaceGeometry("medium", "fit-width", false, 1100, 700);
        var fitHeight = ReaderLayoutPolicy.ResolveSurfaceGeometry("medium", "fit-height", false, 1100, 700);
        var spreadHeight = ReaderLayoutPolicy.ResolveSurfaceGeometry("wide", "fit-height", true, 1100, 700);

        Assert.Equal(1100, fitWidth.MaxWidth);
        Assert.Equal(700, fitWidth.MaxHeight);
        Assert.Equal(1100, fitHeight.MaxWidth);
        Assert.Equal(1100, spreadHeight.MaxWidth);
        Assert.All(new[] { fitWidth, fitHeight, spreadHeight }, value =>
        {
            Assert.InRange(value.MaxWidth, 1, 1100);
            Assert.Equal(700, value.MaxHeight);
        });
    }
}
