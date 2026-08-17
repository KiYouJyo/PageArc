using PageArc.Models;
using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class ReaderCompletionTests
{
    [Fact]
    public void ReaderDefaults_MatchFigmaBaseline()
    {
        var settings = new AppSettings();

        Assert.Equal("light", settings.ReadingTheme);
        Assert.Equal("book", settings.DefaultFont);
        Assert.Equal(1.0, settings.FontScale);
        Assert.Equal(1.75, settings.LineHeight);
        Assert.Equal("medium", settings.PageWidth);
        Assert.False(settings.ContinuousScrolling);
        Assert.True(settings.ShowReadingProgress);
    }

    [Fact]
    public void ReadingData_PersistsSelectedTextAnnotationAndLocator()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-reader-completion-{Guid.NewGuid():N}");
        var file = Path.Combine(root, "reading-data.json");
        Directory.CreateDirectory(root);

        try
        {
            var service = new ReadingDataService(file);
            service.Load();
            var saved = service.SaveAnnotation(new ReaderAnnotation
            {
                BookId = "book-1",
                Locator = new FlowContentLocator(2, 0.42, TextQuote: "selected text"),
                ChapterTitle = "Chapter 3",
                Quote = "selected text",
                Note = "Remember this",
                HighlightColor = "blue"
            });

            var reloaded = new ReadingDataService(file);
            reloaded.Load();
            var annotation = Assert.Single(reloaded.GetAnnotations("book-1"));

            Assert.Equal(saved.Id, annotation.Id);
            Assert.Equal(2, annotation.Locator.SectionIndex);
            Assert.Equal(0.42, annotation.Locator.Fraction, 6);
            Assert.Equal("selected text", annotation.Locator.TextQuote);
            Assert.Equal("Chapter 3", annotation.ChapterTitle);
            Assert.Equal("selected text", annotation.Quote);
            Assert.Equal("Remember this", annotation.Note);
            Assert.Equal("blue", annotation.HighlightColor);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReadingData_BookmarkAndAnnotationRemainIndependent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-reader-completion-{Guid.NewGuid():N}");
        var file = Path.Combine(root, "reading-data.json");
        Directory.CreateDirectory(root);

        try
        {
            var service = new ReadingDataService(file);
            service.Load();
            var locator = new FlowContentLocator(1, 0.25, TextQuote: "same place");
            var bookmark = service.ToggleBookmark("book-2", locator, "Chapter 2", "same place");
            var annotation = service.SaveAnnotation(new ReaderAnnotation
            {
                BookId = "book-2",
                Locator = locator,
                ChapterTitle = "Chapter 2",
                Quote = "same place",
                HighlightColor = "yellow"
            });

            Assert.NotNull(bookmark);
            Assert.Single(service.GetBookmarks("book-2"));
            Assert.Single(service.GetAnnotations("book-2"));

            Assert.True(service.RemoveAnnotation(annotation.Id));
            Assert.Empty(service.GetAnnotations("book-2"));
            Assert.Single(service.GetBookmarks("book-2"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
