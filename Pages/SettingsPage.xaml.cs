using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _loaded;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
        Unloaded += (_, _) => PersistReadingSettings();
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        SelectByTag(LanguageCombo, App.Settings.Current.Language);
        SelectByTag(ThemeCombo, App.Settings.Current.AppTheme);
        SelectByTag(ReadingThemeCombo, App.Settings.Current.ReadingTheme);
        SelectByTag(FontCombo, App.Settings.Current.DefaultFont);
        FontScaleSlider.Value = App.Settings.Current.FontScale;
        LineHeightSlider.Value = App.Settings.Current.LineHeight;
        ContinuousToggle.IsOn = App.Settings.Current.ContinuousScrolling;
        RecentToggle.IsOn = App.Settings.Current.ShowRecentBooks;
        DuplicatesToggle.IsOn = App.Settings.Current.DuplicateDetection;
        LanguageCombo.IsEnabled = true;
        _loaded = true;
    }

    private static void SelectByTag(ComboBox comboBox, string tag)
    {
        comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            ?? comboBox.Items.FirstOrDefault();
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || LanguageCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        if (LanguagePreference.Normalize(tag) == App.Settings.Current.Language) return;

        PersistReadingSettings();
        LanguageCombo.IsEnabled = false;
        try
        {
            var switched = App.Localization.SwitchLanguage(tag);
            if (!switched)
                SelectByTag(LanguageCombo, App.Settings.Current.Language);
        }
        finally
        {
            // Switching to "system" can resolve to the already-active language and therefore
            // does not raise LanguageChanged. Always re-enable the selector on this page too.
            LanguageCombo.IsEnabled = true;
        }
        // On success the existing MainWindow handles LanguageChanged and reloads only the
        // current content page in place. This intentionally mirrors UrbanPlanToolbox:
        // window bounds, NavigationView display mode, pane state and title bar never move.
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || ThemeCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        if (string.Equals(tag, App.Settings.Current.AppTheme, StringComparison.OrdinalIgnoreCase)) return;
        App.Settings.Update(x => x.AppTheme = tag);
        App.MainWindow?.ApplyAppTheme(tag);
    }

    private void PersistReadingSettings()
    {
        if (!_loaded) return;
        App.Settings.Update(settings =>
        {
            if (ReadingThemeCombo.SelectedItem is ComboBoxItem { Tag: string theme }) settings.ReadingTheme = theme;
            if (FontCombo.SelectedItem is ComboBoxItem { Tag: string font }) settings.DefaultFont = font;
            settings.FontScale = FontScaleSlider.Value;
            settings.LineHeight = LineHeightSlider.Value;
            settings.ContinuousScrolling = ContinuousToggle.IsOn;
            settings.ShowRecentBooks = RecentToggle.IsOn;
            settings.DuplicateDetection = DuplicatesToggle.IsOn;
        });
    }

    private void ManageFolders_Click(object sender, RoutedEventArgs e) => App.MainWindow?.NavigateTo("import-folders");

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Directory.Exists(AppPaths.CacheRoot)) Directory.Delete(AppPaths.CacheRoot, true);
            AppPaths.Ensure();
        }
        catch { }
    }
}
