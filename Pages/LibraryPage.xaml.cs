using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PageArc.Models;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class LibraryPage : Page
{
    private LibraryMode _mode = LibraryMode.Library;
    private readonly ObservableCollection<BookEntry> _visibleBooks = [];

    public LibraryPage()
    {
        StartupDiagnostics.Log("LibraryPage constructor entered.");
        try
        {
            InitializeComponent();
            StartupDiagnostics.Log("LibraryPage.InitializeComponent completed.");
            BooksRepeater.ItemsSource = _visibleBooks;
            Loaded += (_, _) =>
            {
                StartupDiagnostics.Log("LibraryPage Loaded event.");
                Refresh();
            };
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("LibraryPage constructor failed", ex);
            throw;
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        StartupDiagnostics.Log("LibraryPage.OnNavigatedTo entered.");
        base.OnNavigatedTo(e);
        if (e.Parameter is LibraryMode mode) _mode = mode;
        ApplyModeText();
        Refresh();
        StartupDiagnostics.Log("LibraryPage.OnNavigatedTo completed.");
    }

    private void ApplyModeText()
    {
        var titleKey = _mode switch
        {
            LibraryMode.Recent => "Recent_Title",
            LibraryMode.Favorites => "Favorites_Title",
            LibraryMode.Collections => "Collections_Title",
            _ => "Library_Title"
        };
        var subtitleKey = _mode switch
        {
            LibraryMode.Recent => "Recent_Subtitle",
            LibraryMode.Favorites => "Favorites_Subtitle",
            LibraryMode.Collections => "Collections_Subtitle",
            _ => "Library_Subtitle"
        };
        PageTitle.Text = App.Localization.GetString(titleKey);
        PageSubtitle.Text = App.Localization.GetString(subtitleKey);
    }

    private void Refresh()
    {
        IEnumerable<BookEntry> books = App.Library.Books;
        books = _mode switch
        {
            LibraryMode.Recent => books.Where(x => x.LastOpenedAt is not null).OrderByDescending(x => x.LastOpenedAt),
            LibraryMode.Favorites => books.Where(x => x.IsFavorite),
            LibraryMode.Collections => books.Where(x => !string.IsNullOrWhiteSpace(x.Collection)),
            _ => books.OrderByDescending(x => x.LastOpenedAt ?? x.AddedAt)
        };
        var query = SearchBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
            books = books.Where(x => x.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || x.Author.Contains(query, StringComparison.CurrentCultureIgnoreCase));

        _visibleBooks.Clear();
        foreach (var book in books) _visibleBooks.Add(book);
        var isEmpty = _visibleBooks.Count == 0;
        EmptyState.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        BooksScrollViewer.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) Refresh();
    }

    private async void ImportBook_Click(object sender, RoutedEventArgs e)
    {
        ImportInfoBar.IsOpen = false;
        try
        {
            var paths = await PickerService.PickEbooksAsync();
            foreach (var path in paths) await App.Library.ImportAsync(path);
            Refresh();
        }
        catch (Exception ex)
        {
            ImportInfoBar.Severity = InfoBarSeverity.Error;
            ImportInfoBar.Message = ex.Message;
            ImportInfoBar.IsOpen = true;
        }
    }

    private async void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickerService.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(folder)) App.MainWindow?.NavigateTo("import-folders");
    }

    private void BookCard_Click(object sender, RoutedEventArgs e)
    {
        ImportInfoBar.IsOpen = false;
        if (sender is not FrameworkElement { DataContext: BookEntry book }) return;

        if (!book.Format.Equals("EPUB", StringComparison.OrdinalIgnoreCase))
        {
            ImportInfoBar.Severity = InfoBarSeverity.Warning;
            ImportInfoBar.Message = App.Localization.GetString("Reader_UnsupportedV01");
            ImportInfoBar.IsOpen = true;
            return;
        }

        try
        {
            if (App.MainWindow?.OpenBook(book) != true)
            {
                ImportInfoBar.Severity = InfoBarSeverity.Error;
                ImportInfoBar.Message = "The reader could not be opened.";
                ImportInfoBar.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            ImportInfoBar.Severity = InfoBarSeverity.Error;
            ImportInfoBar.Message = ex.Message;
            ImportInfoBar.IsOpen = true;
        }
    }
}
