using Xunit;

namespace PageArc.Tests;

public sealed class NavigationPaneStateTests
{
    [Fact]
    public void NavigationPane_UsesOpaqueCyanChromeWithoutActivationOverrides()
    {
        var root = FindRepoRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var mainWindowXaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        var windowCode = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        var navigationLoadedCode = File.ReadAllText(Path.Combine(root, "MainWindow.FigmaConvergence.cs"));

        Assert.Contains("#FFEAF5F5", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#FF132020", appXaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PageArcNavigationPaneRestBrush", appXaml, StringComparison.Ordinal);

        Assert.Contains("Activated += MainWindow_Activated", windowCode, StringComparison.Ordinal);
        Assert.Contains("WindowActivationState.Deactivated", windowCode, StringComparison.Ordinal);
        Assert.Contains("_isWindowActive", windowCode, StringComparison.Ordinal);
        Assert.Contains("PaneBackground = restingBrush", windowCode, StringComparison.Ordinal);
        Assert.Contains("_requestedAppTheme == ElementTheme.Default", windowCode, StringComparison.Ordinal);
        Assert.Contains("AppNavigation.RequestedTheme = requestedTheme", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("AppNavigation.ActualTheme == ElementTheme.Dark", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ColorHelper.FromArgb(255, 26, 35, 35)", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ColorHelper.FromArgb(255, 229, 249, 249)", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("forceActive", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("AppNavigation.IsPaneOpen || AppNavigation.DisplayMode == NavigationViewDisplayMode.Expanded", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("PaneOpening +=", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("PaneClosed +=", windowCode, StringComparison.Ordinal);

        var loadedHandler = navigationLoadedCode.IndexOf("AppNavigation_Loaded", StringComparison.Ordinal);
        var resetTemplatePart = navigationLoadedCode.IndexOf("_navigationSplitView = null", StringComparison.Ordinal);
        var immediateRefresh = navigationLoadedCode.IndexOf("ApplyNavigationPaneBackground();", StringComparison.Ordinal);
        var layoutRefresh = navigationLoadedCode.IndexOf("AppNavigation.LayoutUpdated += AppNavigation_StartupLayoutUpdated", StringComparison.Ordinal);
        Assert.True(loadedHandler >= 0 && resetTemplatePart > loadedHandler,
            "NavigationView.Loaded must invalidate the SplitView cached before applying the startup theme.");
        Assert.True(immediateRefresh > resetTemplatePart && layoutRefresh > immediateRefresh,
            "NavigationView.Loaded must refresh the pane immediately and again after the template layout stabilizes.");

        Assert.Contains("<ResourceDictionary.ThemeDictionaries>", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("<ResourceDictionary x:Key=\"Light\">", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("<ResourceDictionary x:Key=\"Dark\">", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<StaticResource x:Key=\"NavigationViewDefaultPaneBackground\"", mainWindowXaml, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "PageArc.csproj"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("PageArc repository root not found.");
    }
}
