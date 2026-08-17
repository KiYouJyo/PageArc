using Microsoft.UI.Xaml.Controls;

namespace PageArc.Pages;

public sealed partial class SettingsPage
{
    private void ReadingThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || ReadingThemeCombo.SelectedItem is not ComboBoxItem { Tag: string theme }) return;
        App.Settings.Update(settings =>
        {
            settings.ReadingTheme = theme;
            settings.ReadingThemeFollowsApp = false;
        });
    }
}
