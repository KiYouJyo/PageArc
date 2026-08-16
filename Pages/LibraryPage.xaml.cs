using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using PageArc.Models;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class LibraryPage : Page
{
    private LibraryMode _mode = LibraryMode.Library;
    private string? _categoryName;
    private string _filterTag = "all";
    private readonly ObservableCollection<BookEntry> _visibleBooks = [];

    public LibraryPage()
    {
        StartupDiagnostics.Log("LibraryPage constructor entered.");
        try
        {
            InitializeComponent();
            ApplyLibraryStaticText();
            StartupDiagnostics.Log("LibraryPage.InitializeComponent completed.");
            BooksRepeater.ItemsSource = _visibleBooks;
            Loaded += (_, _) =>
            {
                StartupDiagnostics.Log("LibraryPage Loaded event.");
                App.Library.RefreshFileStates();
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
        _categoryName = null;
        if (e.Parameter is LibraryMode mode) _mode = mode;
        if (e.Parameter is string value && value.StartsWith("category:", StringComparison.Ordinal))
        {
            _mode = LibraryMode.Library;
            _categoryName = value["category:".Length..];
        }
        ApplyModeText();
        Refresh();
        StartupDiagnostics.Log("LibraryPage.OnNavigatedTo completed.");
    }

    private void ApplyModeText()
    {
        if (!string.IsNullOrWhiteSpace(_categoryName))
        {
            PageTitle.Text = _categoryName;
            PageSubtitle.Text = App.Localization.GetString("Categories_DetailSubtitle");
            return;
        }

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
        if (!string.IsNullOrWhiteSpace(_categoryName))
            books = books.Where(x => string.Equals(x.Collection, _categoryName, StringComparison.CurrentCultureIgnoreCase));

        books = _mode switch
        {
            LibraryMode.Recent => books.Where(x => x.LastOpenedAt is not null),
            LibraryMode.Favorites => books.Where(x => x.IsFavorite),
            LibraryMode.Collections => books.Where(x => !string.IsNullOrWhiteSpace(x.Collection)),
            _ => books
        };

        books = _filterTag switch
        {
            "recent" => books.OrderByDescending(x => x.AddedAt),
            "progress" => books.Where(x => x.Progress > 0.001 && x.Progress < 0.999),
            "finished" => books.Where(x => x.Progress >= 0.999),
            "favorites" => books.Where(x => x.IsFavorite),
            _ => books
        };

        var query = SearchBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            books = books.Where(x =>
                x.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || x.Author.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || x.Format.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || x.Publisher.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || (x.Collection?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false));
        }

        books = SortComboBox.SelectedIndex == 1
            ? books.OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
            : books.OrderByDescending(x => x.LastOpenedAt ?? x.AddedAt);

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

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string tag } selected) return;
        _filterTag = tag;
        foreach (var button in new[] { FilterAll, FilterRecentlyAdded, FilterInProgress, FilterFinished, FilterFavorites })
            button.IsChecked = ReferenceEquals(button, selected);
        Refresh();
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) Refresh();
    }

    private BookEntry? ResolveBook(object sender)
    {
        if (sender is not FrameworkElement element) return null;
        if (element.Tag is string id && !string.IsNullOrWhiteSpace(id))
            return App.Library.FindById(id);
        return element.DataContext as BookEntry;
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        var book = ResolveBook(sender);
        if (book is null)
        {
            StartupDiagnostics.Log("Favorite click could not resolve BookEntry from card.");
            return;
        }
        ToggleFavorite(book);
    }

    private void ToggleFavorite(BookEntry book)
    {
        book.IsFavorite = !book.IsFavorite;
        App.Library.Save();
        Refresh();
        RefreshDetailsPanel(book);
    }

    private async void BookCard_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement target) return;
        var book = ResolveBook(sender);
        if (book is null)
        {
            StartupDiagnostics.Log("Book right-click could not resolve BookEntry from card.");
            return;
        }
        e.Handled = true;

        var flyout = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = LocalText("打开", "開く", "Open") };
        open.Click += (_, _) => OpenBook(book);
        flyout.Items.Add(open);

        var continueReading = new MenuFlyoutItem { Text = LocalText("继续阅读", "続きを読む", "Continue reading"), IsEnabled = book.Progress > 0 };
        continueReading.Click += (_, _) => OpenBook(book);
        flyout.Items.Add(continueReading);

        var favorite = new MenuFlyoutItem
        {
            Text = book.IsFavorite
                ? LocalText("取消收藏", "お気に入りから削除", "Remove from favorites")
                : LocalText("加入收藏", "お気に入りに追加", "Add to favorites")
        };
        favorite.Click += (_, _) => ToggleFavorite(book);
        flyout.Items.Add(favorite);

        var categories = new MenuFlyoutSubItem { Text = LocalText("加入分类", "カテゴリに追加", "Add to category") };
        foreach (var category in App.Categories.Categories.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var item = new MenuFlyoutItem
            {
                Text = category.Name,
                IsEnabled = !string.Equals(book.Collection, category.Name, StringComparison.CurrentCultureIgnoreCase)
            };
            item.Click += (_, _) =>
            {
                book.Collection = category.Name;
                App.Library.Save();
                Refresh();
                RefreshDetailsPanel(book);
            };
            categories.Items.Add(item);
        }
        if (categories.Items.Count == 0)
            categories.Items.Add(new MenuFlyoutItem { Text = LocalText("暂无分类", "カテゴリなし", "No categories"), IsEnabled = false });
        flyout.Items.Add(categories);

        var details = new MenuFlyoutItem { Text = LocalText("查看详情", "詳細を表示", "View details") };
        details.Click += (_, _) => ShowBookDetailsPanel(book);
        flyout.Items.Add(details);
        flyout.Items.Add(new MenuFlyoutSeparator());

        var location = new MenuFlyoutItem { Text = LocalText("打开文件位置", "ファイルの場所を開く", "Show file location"), IsEnabled = !book.IsMissing };
        location.Click += (_, _) => ShowFileLocation(book);
        flyout.Items.Add(location);

        var remove = new MenuFlyoutItem { Text = LocalText("从书库移除", "ライブラリから削除", "Remove from library") };
        remove.Click += async (_, _) => await ConfirmRemoveAsync(book);
        flyout.Items.Add(remove);

        flyout.ShowAt(target, e.GetPosition(target));
        await Task.CompletedTask;
    }

    private async Task ConfirmRemoveAsync(BookEntry book)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = LocalText("从书库移除？", "ライブラリから削除しますか？", "Remove from library?"),
            Content = LocalText("只会移除 PageArc 中的书库记录，不会删除原始电子书文件。", "PageArc の登録だけを削除し、元の電子書籍ファイルは削除しません。", "This only removes the PageArc library record. The original ebook file will not be deleted."),
            PrimaryButtonText = LocalText("移除", "削除", "Remove"),
            CloseButtonText = LocalText("取消", "キャンセル", "Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (_detailsBook is not null && string.Equals(_detailsBook.Id, book.Id, StringComparison.Ordinal))
        {
            BookDetailsPanel.Visibility = Visibility.Collapsed;
            _detailsBook = null;
        }
        App.Library.Remove(book);
        Refresh();
    }

    private static void ShowFileLocation(BookEntry book)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{book.FilePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Failed to open ebook file location", ex);
        }
    }

    private static string LocalText(string zh, string ja, string en) =>
        App.Settings.Current.Language switch
        {
            "zh-CN" => zh,
            "ja-JP" => ja,
            _ => en
        };

    private async void ImportBook_Click(object sender, RoutedEventArgs e)
    {
        await ShowImportDialogAsync();
    }

    private async void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        await ShowImportDialogAsync(browseFolderImmediately: true);
    }

    private void BookCard_Click(object sender, RoutedEventArgs e)
    {
        ImportInfoBar.IsOpen = false;
        var book = ResolveBook(sender);
        if (book is null)
        {
            StartupDiagnostics.Log("BookCard_Click fired but the BookEntry could not be resolved.");
            ImportInfoBar.Severity = InfoBarSeverity.Error;
            ImportInfoBar.Message = LocalText("无法识别这本书，请重新导入。", "この本を特定できません。再インポートしてください。", "PageArc could not resolve this book. Please re-import it.");
            ImportInfoBar.IsOpen = true;
            return;
        }

        StartupDiagnostics.Log($"BookCard_Click resolved '{book.Title}' ({book.Id}); opening reader.");
        OpenBook(book);
    }

    private void OpenBook(BookEntry book)
    {
        ImportInfoBar.IsOpen = false;
        if (book.IsMissing || !File.Exists(book.FilePath))
        {
            book.IsMissing = true;
            App.Library.Save();
            ImportInfoBar.Severity = InfoBarSeverity.Error;
            ImportInfoBar.Message = LocalText("找不到原始电子书文件。请恢复文件或重新导入。", "元の電子書籍ファイルが見つかりません。ファイルを復元するか再インポートしてください。", "The original ebook file is missing. Restore it or import the book again.");
            ImportInfoBar.IsOpen = true;
            return;
        }

        try
        {
            StartupDiagnostics.Log($"Library requesting reader navigation for '{book.Title}' ({book.Id}, {book.Format}).");
            if (App.MainWindow?.OpenBook(book) != true)
            {
                StartupDiagnostics.Log("MainWindow.OpenBook returned false or MainWindow was unavailable.");
                ImportInfoBar.Severity = InfoBarSeverity.Error;
                ImportInfoBar.Message = App.Localization.GetString("Reader_OpenFailed");
                ImportInfoBar.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Library OpenBook failed", ex);
            ImportInfoBar.Severity = InfoBarSeverity.Error;
            ImportInfoBar.Message = ex.Message;
            ImportInfoBar.IsOpen = true;
        }
    }
}
