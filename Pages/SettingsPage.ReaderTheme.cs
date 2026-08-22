using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace PageArc.Pages;

public sealed partial class SettingsPage
{
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ReadingThemeCombo.SelectionChanged -= ReadingThemeCombo_SelectionChanged;
        ReadingThemeCombo.SelectionChanged += ReadingThemeCombo_SelectionChanged;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ReadingThemeCombo.SelectionChanged -= ReadingThemeCombo_SelectionChanged;
        base.OnNavigatedFrom(e);
    }

    private void ReadingThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || ReadingThemeCombo.SelectedItem is not ComboBoxItem { Tag: string theme }) return;
        App.Settings.Update(settings =>
        {
            settings.ReadingThemeFollowsApp = theme == "app";
            settings.ReadingTheme = theme == "app"
                ? (ActualTheme == ElementTheme.Dark ? "dark" : "light")
                : theme;
        });
    }
}
