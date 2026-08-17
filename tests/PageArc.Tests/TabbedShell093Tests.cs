using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class TabbedShell093Tests
{
    [Fact]
    public void StartupAndNewTabCanCreateMultipleHomeSessions()
    {
        var manager = new ShellTabSessionManager();
        var first = manager.CreateHome();
        var second = manager.CreateHome();

        Assert.NotEqual(first.Id, second.Id);
        Assert.All(manager.Tabs, tab => Assert.Equal(ShellTabKind.Home, tab.Kind));
    }

    [Fact]
    public void OpeningSameBookReusesExistingReaderTab()
    {
        var manager = new ShellTabSessionManager();
        manager.CreateHome();
        var first = manager.OpenReader("book-1");
        var second = manager.OpenReader("book-1");
        var other = manager.OpenReader("book-2");

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Session.Id, second.Session.Id);
        Assert.True(other.Created);
        Assert.Equal(3, manager.Tabs.Count);
    }

    [Fact]
    public void ClosingLastTabCanRecoverAHomeSession()
    {
        var manager = new ShellTabSessionManager();
        var home = manager.CreateHome();
        Assert.True(manager.Close(home.Id));

        var recovered = manager.EnsureHomeIfEmpty();
        Assert.Equal(ShellTabKind.Home, recovered.Kind);
        Assert.Single(manager.Tabs);
    }

    [Fact]
    public void FlowPageMapProvidesStableOneBasedNavigation()
    {
        var document = new FlowDocument
        {
            Format = "EPUB",
            Title = "Test",
            Sections =
            [
                new FlowSection("a", "a.xhtml", "application/xhtml+xml", Size: 7000),
                new FlowSection("b", "b.xhtml", "application/xhtml+xml", Size: 3500),
                new FlowSection("c", "c.xhtml", "application/xhtml+xml", Size: 1)
            ]
        };

        var map = new FlowPageMap(document);

        Assert.Equal(4, map.TotalPages);
        Assert.Equal(1, map.GetPage(0, 0));
        Assert.Equal(2, map.GetPage(0, 0.75));
        Assert.Equal(3, map.GetPage(1, 0));
        Assert.Equal(4, map.GetPage(2, 1));

        var third = map.LocatePage(3);
        Assert.Equal(1, third.SectionIndex);
        Assert.Equal(0d, third.Fraction);
    }

    [Fact]
    public void FlowPageMapMapsGlobalProgressToReaderLocator()
    {
        var document = new FlowDocument
        {
            Format = "FB2",
            Title = "Test",
            Sections =
            [
                new FlowSection("a", "a", "text/html", Size: 3500),
                new FlowSection("b", "b", "text/html", Size: 10500)
            ]
        };

        var map = new FlowPageMap(document);
        var nearEnd = map.LocateProgress(0.9);

        Assert.Equal(4, map.TotalPages);
        Assert.Equal(1, nearEnd.SectionIndex);
        Assert.InRange(nearEnd.Fraction, 0.6, 0.8);
    }
}
