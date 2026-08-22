using System.Diagnostics;

namespace PageArc.Services;

/// <summary>Defines the same non-blocking startup timing contract used by UrbanPlanToolbox.</summary>
public static class StartupSplashTiming
{
    public static readonly TimeSpan MinimumVisibleDuration = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan FadeOutFallbackDuration = TimeSpan.FromMilliseconds(300);

    public static TimeSpan ResolveVisibleDuration(TimeSpan initializationDuration) =>
        initializationDuration > MinimumVisibleDuration ? initializationDuration : MinimumVisibleDuration;

    public static TimeSpan RemainingMinimumVisibleDuration(Stopwatch visibleClock) =>
        MinimumVisibleDuration - visibleClock.Elapsed > TimeSpan.Zero
            ? MinimumVisibleDuration - visibleClock.Elapsed
            : TimeSpan.Zero;
}
