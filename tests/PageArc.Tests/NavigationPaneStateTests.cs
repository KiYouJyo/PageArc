using Xunit;

namespace PageArc.Tests;

public sealed class NavigationPaneStateTests
{
    [Fact]
    public void NavigationPane_SharesWindowMicaLikeSpatialViewer()
    {
        var root = FindRepoRoot();
        var appXaml = File.ReadAllText(Path.Combine(root, "App.xaml"));
        var mainWindowXaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        var windowCode = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));

        Assert.Contains("x:Key=\"PageArcNavigationPaneBrush\" Color=\"Transparent\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"PageArcNavigationPaneRestBrush\" Color=\"Transparent\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("<StaticResource x:Key=\"NavigationViewDefaultPaneBackground\" ResourceKey=\"PageArcNavigationPaneBrush\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("NavigationViewBorderThickness\">0", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("NavigationViewPaneContentGridMargin\">0", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Background=\"{ThemeResource PageArcToolbarBrush}\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ResourceDictionary.ThemeDictionaries>", mainWindowXaml, StringComparison.Ordinal);

        // The legacy startup synchronization may still touch the internal SplitView while the
        // NavigationView template settles, but it can now only assign the transparent resting
        // brush. It must never synthesize a cyan active/inactive pane color.
        Assert.Contains("PageArcNavigationPaneRestBrush", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ColorHelper.FromArgb(255, 26, 35, 35)", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ColorHelper.FromArgb(255, 229, 249, 249)", windowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("forceActive", windowCode, StringComparison.Ordinal);
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
