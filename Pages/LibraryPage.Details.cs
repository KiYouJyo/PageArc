using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PageArc.Models;

namespace PageArc.Pages;

public sealed partial class LibraryPage
{
    private BookEntry? _detailsBook;

    private void ApplyLibraryStaticText()
    {
        DetailsHeaderText.Text = LocalText("书籍详情", "書籍詳細", "Book details");
        DetailsContinueText.Text = LocalText("继续阅读", "続きを読む", "Continue reading");
        DetailsFileInfoHeader.Text = LocalText("文件信息", "ファイル情報", "File information");
        DetailsFormatLabel.Text = LocalText("格式", "形式", "Format");
        DetailsSizeLabel.Text = LocalText("文件大小", "ファイルサイズ", "File size");
        DetailsAddedLabel.Text = LocalText("添加时间", "追加日時", "Added");
        DetailsLastOpenedLabel.Text = LocalText("最近打开", "最近開いた日時", "Last opened");
        DetailsLocationLabel.Text = LocalText("位置", "場所", "Location");
        DetailsReadingDataHeader.Text = LocalText("阅读数据", "読書データ", "Reading data");
    }

    private void ShowBookDetailsPanel(BookEntry book)
    {
        _detailsBook = book;
        PopulateBookDetails(book);
        BookDetailsPanel.Visibility = Visibility.Visible;
    }

    private void PopulateBookDetails(BookEntry book)
    {
        DetailsCoverMonogram.Text = book.CoverMonogram;
        DetailsCoverImage.Source = null;
        DetailsCoverImage.Opacity = 0;
        _ = LoadDetailsCoverAsync(book);
        DetailsBookTitle.Text = book.Title;
        DetailsAuthor.Text = book.DisplayAuthor;
        DetailsFormatSize.Text = $"{book.Format} · {book.DisplayFileSize}";
        DetailsProgressBar.Value = Math.Clamp(book.Progress, 0, 1);
        var percent = (int)Math.Round(Math.Clamp(book.Progress, 0, 1) * 100);
        DetailsProgressText.Text = App.Settings.Current.Language switch
        {
            "zh-CN" => $"已读 {percent}%",
            "ja-JP" => $"{percent}% 読了",
            _ => $"{percent}% read"
        };

        DetailsFormatValue.Text = book.Format;
        DetailsSizeValue.Text = book.DisplayFileSize;
        DetailsAddedValue.Text = book.AddedAt.ToLocalTime().ToString("d");
        DetailsLastOpenedValue.Text = book.LastOpenedAt?.ToLocalTime().ToString("g") ?? "—";
        DetailsLocationValue.Text = LocalText("本地书库", "ローカルライブラリ", "Local library");
        DetailsContinueText.Text = book.Progress > 0.001
            ? LocalText("继续阅读", "続きを読む", "Continue reading")
            : LocalText("开始阅读", "読み始める", "Start reading");
        DetailsFavoriteText.Text = book.IsFavorite
            ? LocalText("★ 已收藏", "★ お気に入り", "★ Favorited")
            : LocalText("☆ 收藏", "☆ お気に入り", "☆ Favorite");

        var bookmarks = App.ReadingData.GetBookmarks(book.Id).Count;
        var annotations = App.ReadingData.GetAnnotations(book.Id);
        var highlights = annotations.Count;
        var notes = annotations.Count(x => !string.IsNullOrWhiteSpace(x.Note));
        DetailsReadingDataSummary.Text = App.Settings.Current.Language switch
        {
            "zh-CN" => $"{bookmarks} 个书签 · {highlights} 处高亮 · {notes} 条笔记",
            "ja-JP" => $"ブックマーク {bookmarks} · ハイライト {highlights} · ノート {notes}",
            _ => $"{bookmarks} bookmarks · {highlights} highlights · {notes} notes"
        };
    }

    private void RefreshDetailsPanel(BookEntry book)
    {
        if (_detailsBook is not null && string.Equals(_detailsBook.Id, book.Id, StringComparison.Ordinal))
            PopulateBookDetails(book);
    }

    private void DetailsClose_Click(object sender, RoutedEventArgs e)
    {
        BookDetailsPanel.Visibility = Visibility.Collapsed;
        DetailsCoverImage.Source = null;
        DetailsCoverImage.Opacity = 0;
        _detailsBook = null;
    }

    private void DetailsContinue_Click(object sender, RoutedEventArgs e)
    {
        if (_detailsBook is not null) OpenBook(_detailsBook);
    }

    private void DetailsFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (_detailsBook is not null) ToggleFavorite(_detailsBook);
    }
}
