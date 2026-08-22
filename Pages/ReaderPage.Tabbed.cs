using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using PageArc.Models;
using PageArc.Services;
using Windows.System;

namespace PageArc.Pages;

public sealed partial class ReaderPage
{
    private readonly Button ContentsButton = new();
    private readonly Button SearchButton = new();
    private readonly Button BookmarkButton = new();
    private readonly TextBlock BookmarkToolText = new();

    private string _unifiedSidebarMode = "contents";
    private FlowPageMap? _pageMap;
    private CancellationTokenSource? _progressSeekCts;
    private bool _readerSessionClosed;
    private bool _chromeInitialized;
    private int? _measuredAbsolutePage;

    private void InitializeTabbedReaderChrome()
    {
        if (_chromeInitialized) return;
        _chromeInitialized = true;

        ContentsModeLabel.Text = ReaderText("目录", "目次", "Contents");
        SearchModeLabel.Text = ReaderText("搜索", "検索", "Search");
        BookmarksModeLabel.Text = ReaderText("书签", "しおり", "Bookmarks");
        NotesModeLabel.Text = ReaderText("笔记", "ノート", "Notes");
        SearchHeading.Text = SearchModeLabel.Text;
        BookmarksHeading.Text = BookmarksModeLabel.Text;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(BookmarkButton, BookmarksModeLabel.Text);
        AddCurrentBookmarkButton.Content = ReaderText("＋ 添加当前页书签", "＋ 現在位置をしおりに追加", "+ Bookmark current page");
        PageJumpLabel.Text = ReaderText("页", "ページ", "Page");
        UpdateUnifiedSidebarVisuals();
        UpdatePageJumpUi();
        ApplyTabbedProgressVisibility();

        ReaderWebView.NavigationCompleted += (_, args) =>
        {
            if (args.IsSuccess) DispatcherQueue.TryEnqueue(UpdatePageJumpUi);
        };
    }

    private void ApplyTabbedProgressVisibility() => ReaderProgressStrip.Visibility = Visibility.Visible;

    public void PrepareForClose()
    {
        if (_readerSessionClosed) return;
        _readerSessionClosed = true;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
        _progressSeekCts?.Cancel();
        _progressSeekCts?.Dispose();
        _progressSeekCts = null;
        _leftPaneAnimationCts?.Cancel();
        _rightPaneAnimationCts?.Cancel();
        SaveReadingPosition(force: true);
        var source = _source;
        _source = null;
        if (source is not null) _ = source.DisposeAsync().AsTask();
    }

    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        var open = ContentsColumn.Width.GridUnitType != GridUnitType.Pixel || ContentsColumn.Width.Value <= 0.5;
        _ = AnimateLeftSidebarAsync(open);
    }

    private void SidebarModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string mode }) return;
        ShowUnifiedSidebar(mode);
    }

    private void ShowUnifiedSidebar(string mode)
    {
        mode = mode is "search" or "bookmarks" or "notes" ? mode : "contents";
        _unifiedSidebarMode = mode;
        ContentsMode.Visibility = mode == "contents" ? Visibility.Visible : Visibility.Collapsed;
        SearchMode.Visibility = mode == "search" ? Visibility.Visible : Visibility.Collapsed;
        BookmarksMode.Visibility = mode == "bookmarks" ? Visibility.Visible : Visibility.Collapsed;
        NotesMode.Visibility = mode == "notes" ? Visibility.Visible : Visibility.Collapsed;

        if (mode == "contents") _sidebarMode = ReaderSidebarMode.Contents;
        else if (mode == "search") _sidebarMode = ReaderSidebarMode.Search;
        else _sidebarMode = ReaderSidebarMode.Bookmarks;

        if (mode == "bookmarks") RefreshBookmarks();
        if (mode == "notes") RefreshAnnotations();
        if (mode == "search") ReaderSearchBox.Focus(FocusState.Programmatic);
        UpdateUnifiedSidebarVisuals();
        _ = AnimateLeftSidebarAsync(true);
    }

    private void UpdateUnifiedSidebarVisuals()
    {
        SetSidebarButtonState(ContentsModeButton, _unifiedSidebarMode == "contents");
        SetSidebarButtonState(SearchModeButton, _unifiedSidebarMode == "search");
        SetSidebarButtonState(BookmarksModeButton, _unifiedSidebarMode == "bookmarks");
        SetSidebarButtonState(NotesModeButton, _unifiedSidebarMode == "notes");
    }

    private void SetSidebarButtonState(Button button, bool selected)
    {
        var dark = ReaderRootGrid.ActualTheme == ElementTheme.Dark;
        button.Background = selected
            ? new SolidColorBrush(dark ? ColorHelper.FromArgb(24, 255, 255, 255) : ColorHelper.FromArgb(13, 0, 0, 0))
            : new SolidColorBrush(Colors.Transparent);
        if (button.Content is TextBlock text)
            text.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        if (selected) AnimateReaderSelection(button);
    }

    private static void AnimateReaderSelection(UIElement element)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0, 0.76f);
        animation.InsertKeyFrame(1, 1f);
        animation.Duration = TimeSpan.FromMilliseconds(140);
        visual.StartAnimation("Opacity", animation);
    }

    private void AddCurrentBookmark_Click(object sender, RoutedEventArgs e)
    {
        if (_book is null || _document is null) return;
        var snippet = BuildCurrentSnippet();
        var locator = new FlowContentLocator(_sectionIndex, _sectionFraction, TextQuote: snippet);
        var chapterTitle = FlowSearchService.ResolveChapterTitle(_document, _sectionIndex);
        var bookmark = App.ReadingData.ToggleBookmark(_book.Id, locator, chapterTitle, snippet);
        RefreshBookmarks();
        if (bookmark is not null)
        {
            ReaderInfoBar.Severity = InfoBarSeverity.Success;
            ReaderInfoBar.Message = App.Localization.GetString("Reader_BookmarkSaved");
            ReaderInfoBar.IsOpen = true;
        }
    }

    private FlowPageMap? EnsurePageMap()
    {
        if (_document is null) return null;
        return _pageMap ??= new FlowPageMap(_document);
    }

    private void UpdatePageJumpUi()
    {
        var map = EnsurePageMap();
        if (map is null)
        {
            PageJumpBox.Text = string.Empty;
            PageTotalText.Text = "/ —";
            return;
        }

        var page = _measuredAbsolutePage ?? map.GetPage(_sectionIndex, _sectionFraction);
        PageJumpBox.Text = Math.Clamp(page, 1, map.TotalPages).ToString(CultureInfo.CurrentCulture);
        PageTotalText.Text = $"/ {map.TotalPages.ToString(CultureInfo.CurrentCulture)}";
    }

    private void ReaderProgress_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdatePageJumpUi();
        if (_document is null || _book is null) return;
        if (Math.Abs(e.NewValue - _book.Progress) < 0.0005) return;

        _progressSeekCts?.Cancel();
        _progressSeekCts?.Dispose();
        _progressSeekCts = new CancellationTokenSource();
        _ = SeekProgressAfterDelayAsync(e.NewValue, _progressSeekCts.Token);
    }

    private async Task SeekProgressAfterDelayAsync(double progress, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(120, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var map = EnsurePageMap();
            if (map is null) return;
            var locator = map.LocateProgress(progress);
            _sectionFraction = locator.Fraction;
            await NavigateToSectionAsync(locator.SectionIndex, restoreSavedFraction: true);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void PageJumpBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        await JumpFromPageBoxAsync();
    }

    private async void PageJumpBox_LostFocus(object sender, RoutedEventArgs e) => await JumpFromPageBoxAsync();

    private async Task JumpFromPageBoxAsync()
    {
        var map = EnsurePageMap();
        if (map is null) return;
        if (!int.TryParse(PageJumpBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var page))
        {
            UpdatePageJumpUi();
            return;
        }

        page = Math.Clamp(page, 1, map.TotalPages);
        var locator = map.LocatePage(page);
        _sectionFraction = locator.Fraction;
        await NavigateToSectionAsync(locator.SectionIndex, restoreSavedFraction: true);
        UpdatePageJumpUi();
    }
}
