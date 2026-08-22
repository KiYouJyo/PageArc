using PageArc.Services;
using Xunit;

namespace PageArc.Tests;

public sealed class ReaderSettingsPersistenceTests
{
    [Fact]
    public void CompletedReaderSettings_RoundTripThroughSettingsService()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pagearc-reader-settings-{Guid.NewGuid():N}");
        var file = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(root);

        try
        {
            var settings = new SettingsService(file);
            settings.Load();
            settings.Update(value =>
            {
                value.ReadingTheme = "dark";
                value.DefaultFont = "Georgia";
                value.FontScale = 1.2;
                value.LineHeight = 2.0;
                value.PageWidth = "wide";
                value.ContinuousScrolling = true;
                value.ShowReadingProgress = false;
                value.ClickToTurnPages = false;
            });

            var reloaded = new SettingsService(file);
            reloaded.Load();

            Assert.Equal("dark", reloaded.Current.ReadingTheme);
            Assert.Equal("Georgia", reloaded.Current.DefaultFont);
            Assert.Equal(1.2, reloaded.Current.FontScale, 6);
            Assert.Equal(2.0, reloaded.Current.LineHeight, 6);
            Assert.Equal("wide", reloaded.Current.PageWidth);
            Assert.True(reloaded.Current.ContinuousScrolling);
            Assert.False(reloaded.Current.ShowReadingProgress);
            Assert.False(reloaded.Current.ClickToTurnPages);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
