using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class StartupSplashTests
{
    [Theory]
    [InlineData(50, 500)]
    [InlineData(300, 500)]
    [InlineData(500, 500)]
    [InlineData(900, 900)]
    public void VisibleDurationIsTheMaximumOfMinimumAndInitialization(int initializationMilliseconds, int expectedMilliseconds)
    {
        var duration = StartupSplashTiming.ResolveVisibleDuration(TimeSpan.FromMilliseconds(initializationMilliseconds));
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), duration);
    }

    [Fact]
    public void FadeOutMatchesTheUrbanPlanToolboxContract()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(500), StartupSplashTiming.MinimumVisibleDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(200), StartupSplashTiming.FadeOutDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(300), StartupSplashTiming.FadeOutFallbackDuration);
        Assert.Equal("ms-appx:///Assets/Icon-Large-1024.png", StartupSplashPresentation.LogoAssetUri);
        Assert.Equal(183, StartupSplashPresentation.LogoCanvasSize);
    }
}
