using Microsoft.UI.Xaml.Controls.Primitives;

namespace PageArc.Pages;

public sealed partial class LibraryPage
{
    private bool _restoringLibraryPreferences;

    private void RestoreLibraryPreferences()
    {
        _restoringLibraryPreferences = true;
        try
        {
            _filterTag = App.Settings.Current.LibraryFilter switch
            {
                "recent" => "recent",
                "progress" => "progress",
                "finished" => "finished",
                "favorites" => "favorites",
                _ => "all"
            };
            foreach (var button in new[] { FilterAll, FilterRecentlyAdded, FilterInProgress, FilterFinished, FilterFavorites })
                button.IsChecked = string.Equals(button.Tag as string, _filterTag, StringComparison.Ordinal);

            SortComboBox.SelectedIndex = string.Equals(App.Settings.Current.LibrarySort, "title", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }
        finally
        {
            _restoringLibraryPreferences = false;
        }
    }

    private void PersistLibraryFilterPreference()
    {
        if (_restoringLibraryPreferences) return;
        App.Settings.Update(settings => settings.LibraryFilter = _filterTag);
    }

    private void PersistLibrarySortPreference()
    {
        if (_restoringLibraryPreferences) return;
        var value = SortComboBox.SelectedIndex == 1 ? "title" : "recent";
        App.Settings.Update(settings => settings.LibrarySort = value);
    }
}
