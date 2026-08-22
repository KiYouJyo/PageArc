using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using PageArc.Services;

namespace PageArc.Pages;

public sealed partial class ReaderPage
{
    private bool _figmaReaderControlsReady;
    private bool _figmaThemeHooked;
    private bool _readingSettingsPaneOpen;
    private int _readerFrameGeometryRevision;

    private void ReaderPage_FigmaLoaded(object sender, RoutedEventArgs e)
    {
        // Figma 16:156 / 44:2: the window owns navigation tabs; the reader owns only
        // its local sidebar, document controls and reading surface.
        SyncFollowAppReaderTheme();
        InitializeFigmaReaderControls();
        InitializeTabbedReaderChrome();
        ReaderPage_NotesLoaded(sender, e);
        InitializeSelectionAnnotationUi();
        InitializeRefinedReaderUi();

        if (!_figmaThemeHooked)
        {
            _figmaThemeHooked = true;
            ReaderRootGrid.ActualThemeChanged += ReaderRootGrid_ActualThemeChanged;
        }
    }

    private void InitializeFigmaReaderControls()
    {
        EnforceFixedReaderOptions();
        var settings = App.Settings.Current;

        var previousSettingsReady = _settingsReady;
        _settingsReady = false;
        _figmaReaderControlsReady = false;
        try
        {
            SelectByTag(ReaderThemeCombo, EffectiveReaderTheme());
            ReaderFontScaleSlider.Value = settings.FontScale;
            ReaderLineHeightSlider.Value = settings.LineHeight;
            SelectByTag(FigmaFontFamilyCombo, settings.DefaultFont);
            ClickPageTurnToggle.IsOn = settings.ClickToTurnPages;
        }
        finally
        {
            _settingsReady = previousSettingsReady;
            _figmaReaderControlsReady = true;
        }

        ReaderFontLabel.Text = ReaderText("字体", "フォント", "Font");
        ThemeLightText.Text = ReaderText("浅色", "ライト", "Light");
        ThemeSepiaText.Text = ReaderText("米黄色", "セピア", "Sepia");
        ThemeDarkText.Text = ReaderText("深色", "ダーク", "Dark");
        LineCompactText.Text = ReaderText("紧凑", "狭い", "Compact");
        LineNormalText.Text = ReaderText("标准", "標準", "Normal");
        LineRelaxedText.Text = ReaderText("宽松", "広い", "Relaxed");
        ReaderViewOptionsLabel.Text = ReaderText("查看选项", "表示オプション", "View options");
        SinglePageText.Text = ReaderText("单页视图", "単ページ表示", "Single page");
        OddPageStartText.Text = ReaderText("奇数页起始\n（无封面）", "奇数ページ開始\n（表紙なし）", "Odd-page start\n(No cover)");
        EvenPageStartText.Text = ReaderText("偶数页起始\n（有封面）", "偶数ページ開始\n（表紙あり）", "Even-page start\n(With cover)");
        FitPageWidthText.Text = ReaderText("适应页面宽度", "ページ幅に合わせる", "Fit page width");
        FitPageHeightText.Text = ReaderText("适应页面高度", "ページ高さに合わせる", "Fit page height");
        ClickPageTurnToggle.Header = ReaderText("点击页面翻页", "ページのクリックで移動", "Click page to turn");
        ClickPageTurnToggle.OnContent = ReaderText("开", "オン", "On");
        ClickPageTurnToggle.OffContent = ReaderText("关", "オフ", "Off");
        FigmaResetReaderButton.Content = ReaderText("恢复默认设置", "既定の設定に戻す", "Restore defaults");

        SetReadingSettingsPaneOpen(false);
        UpdateFigmaReaderSelectionVisuals();
        UpdateReaderViewOptionSelection();
        ApplyFigmaReaderPageGeometry();
        ApplyFigmaReaderSurfaceTheme();
        ApplyProgressVisibility();
    }

    private async void ReaderRootGrid_ActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateUnifiedSidebarVisuals();
        if (!App.Settings.Current.ReadingThemeFollowsApp) return;
        SyncFollowAppReaderTheme();

        var previousSettingsReady = _settingsReady;
        _settingsReady = false;
        try
        {
            SelectByTag(ReaderThemeCombo, EffectiveReaderTheme());
        }
        finally
        {
            _settingsReady = previousSettingsReady;
        }

        UpdateFigmaReaderSelectionVisuals();
        ApplyFigmaReaderSurfaceTheme();
        await RefreshReaderPresentationAsync();
    }

    private void SyncFollowAppReaderTheme()
    {
        if (!App.Settings.Current.ReadingThemeFollowsApp) return;
        var target = ReaderRootGrid.ActualTheme == ElementTheme.Dark ? "dark" : "light";
        if (string.Equals(target, App.Settings.Current.ReadingTheme, StringComparison.OrdinalIgnoreCase)) return;
        App.Settings.Update(settings => settings.ReadingTheme = target);
    }

    private string EffectiveReaderTheme()
    {
        var settings = App.Settings.Current;
        if (settings.ReadingThemeFollowsApp)
            return ReaderRootGrid.ActualTheme == ElementTheme.Dark ? "dark" : "light";
        return settings.ReadingTheme switch
        {
            "dark" => "dark",
            "sepia" => "sepia",
            _ => "light"
        };
    }

    private void AppearanceButton_Click(object sender, RoutedEventArgs e) =>
        SetReadingSettingsPaneOpen(!_readingSettingsPaneOpen);

    private void ReaderSettingsClose_Click(object sender, RoutedEventArgs e) => SetReadingSettingsPaneOpen(false);

    private void SetReadingSettingsPaneOpen(bool open)
    {
        _readingSettingsPaneOpen = open;
        _ = AnimateRightSidebarAsync(open);
        AppearanceButton.Background = open
            ? new SolidColorBrush(ReaderRootGrid.ActualTheme == ElementTheme.Dark
                ? ColorHelper.FromArgb(24, 255, 255, 255)
                : ColorHelper.FromArgb(13, 0, 0, 0))
            : new SolidColorBrush(Colors.Transparent);
    }

    private async void ReaderThemeCard_Click(object sender, RoutedEventArgs e)
    {
        if (!_figmaReaderControlsReady || sender is not Button { Tag: string theme }) return;
        theme = theme is "dark" or "sepia" ? theme : "light";
        App.Settings.Update(settings =>
        {
            settings.ReadingTheme = theme;
            settings.ReadingThemeFollowsApp = false;
        });

        var previousSettingsReady = _settingsReady;
        _settingsReady = false;
        try
        {
            SelectByTag(ReaderThemeCombo, theme);
        }
        finally
        {
            _settingsReady = previousSettingsReady;
        }

        UpdateFigmaReaderSelectionVisuals();
        ApplyFigmaReaderSurfaceTheme();
        await RefreshReaderPresentationAsync();
    }

    private async void FigmaFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_figmaReaderControlsReady || FigmaFontFamilyCombo.SelectedItem is not ComboBoxItem { Tag: string font }) return;
        App.Settings.Update(settings => settings.DefaultFont = font);
        await RefreshReaderPresentationAsync();
    }

    private async void ReaderLineSpacing_Click(object sender, RoutedEventArgs e)
    {
        if (!_figmaReaderControlsReady || sender is not Button { Tag: string raw }
            || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var lineHeight)) return;
        App.Settings.Update(settings => settings.LineHeight = lineHeight);

        var previousSettingsReady = _settingsReady;
        _settingsReady = false;
        try
        {
            ReaderLineHeightSlider.Value = lineHeight;
        }
        finally
        {
            _settingsReady = previousSettingsReady;
        }

        UpdateFigmaReaderSelectionVisuals();
        await RefreshReaderPresentationAsync();
    }

    private async void FigmaReaderReset_Click(object sender, RoutedEventArgs e)
    {
        _figmaReaderControlsReady = false;
        var previousSettingsReady = _settingsReady;
        _settingsReady = false;
        try
        {
            App.Settings.Update(settings =>
            {
                settings.ReadingThemeFollowsApp = true;
                settings.ReadingTheme = ReaderRootGrid.ActualTheme == ElementTheme.Dark ? "dark" : "light";
                settings.DefaultFont = "book";
                settings.FontScale = 1.0;
                settings.LineHeight = 1.75;
                settings.PageWidth = "medium";
                settings.ReaderViewMode = "vertical";
                settings.ReaderSpreadMode = "single";
                settings.ReaderZoomMode = "auto";
                settings.ReaderZoomFactor = 1.0;
                settings.ContinuousScrolling = true;
                settings.ShowReadingProgress = true;
                settings.ClickToTurnPages = true;
            });

            SelectByTag(ReaderThemeCombo, EffectiveReaderTheme());
            SelectByTag(FigmaFontFamilyCombo, "book");
            ReaderFontScaleSlider.Value = 1.0;
            ReaderLineHeightSlider.Value = 1.75;
            ClickPageTurnToggle.IsOn = true;
        }
        finally
        {
            _settingsReady = previousSettingsReady;
            _figmaReaderControlsReady = true;
        }

        UpdateFigmaReaderSelectionVisuals();
        UpdateReaderViewOptionSelection();
        ApplyFigmaReaderPageGeometry();
        ApplyFigmaReaderSurfaceTheme();
        ApplyProgressVisibility();
        await RefreshReaderPresentationAsync();
    }

    private void UpdateFigmaReaderSelectionVisuals()
    {
        var theme = EffectiveReaderTheme();
        SetThemeCardSelection(ThemeLightButton, theme == "light");
        SetThemeCardSelection(ThemeSepiaButton, theme == "sepia");
        SetThemeCardSelection(ThemeDarkButton, theme == "dark");

        var lineHeight = App.Settings.Current.LineHeight;
        SetSegmentSelection(LineCompactButton, Math.Abs(lineHeight - 1.5) < 0.08);
        SetSegmentSelection(LineNormalButton, Math.Abs(lineHeight - 1.75) < 0.08);
        SetSegmentSelection(LineRelaxedButton, Math.Abs(lineHeight - 2.0) < 0.11);

        var settings = App.Settings.Current;
        SetSegmentSelection(SinglePageButton, settings.ReaderViewMode == "horizontal" && settings.ReaderSpreadMode == "single");
    }

    private void EnforceFixedReaderOptions()
    {
        var settings = App.Settings.Current;
        if (settings.ContinuousScrolling && settings.ShowReadingProgress) return;

        App.Settings.Update(value =>
        {
            value.ContinuousScrolling = true;
            value.ShowReadingProgress = true;
        });
    }

    private static void SetThemeCardSelection(Button button, bool selected)
    {
        button.BorderThickness = new Thickness(2);
        button.BorderBrush = selected
            ? new SolidColorBrush(ColorHelper.FromArgb(255, 0, 95, 184))
            : new SolidColorBrush(ColorHelper.FromArgb(72, 117, 117, 117));
        if (selected) AnimateReaderSelection(button);
    }

    private void SetSegmentSelection(Button button, bool selected)
    {
        var dark = ReaderRootGrid.ActualTheme == ElementTheme.Dark;
        button.Background = selected
            ? new SolidColorBrush(dark ? ColorHelper.FromArgb(22, 255, 255, 255) : ColorHelper.FromArgb(13, 0, 0, 0))
            : new SolidColorBrush(Colors.Transparent);
        if (button.Content is TextBlock text)
            text.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private void ApplyFigmaReaderPageGeometry()
    {
        const double logicalWidth = 1600d;
        const double logicalHeight = 900d;

        var availableWidth = Math.Max(1d, ReadingArea.ActualWidth - 48d);
        var availableHeight = Math.Max(1d, ReadingArea.ActualHeight - 88d);
        var oldExtentWidth = ReaderFrameViewport.ExtentWidth;
        var oldExtentHeight = ReaderFrameViewport.ExtentHeight;
        var centerX = oldExtentWidth > 1d
            ? (ReaderFrameViewport.HorizontalOffset + (ReaderFrameViewport.ViewportWidth / 2d)) / oldExtentWidth
            : 0.5d;
        var centerY = oldExtentHeight > 1d
            ? (ReaderFrameViewport.VerticalOffset + (ReaderFrameViewport.ViewportHeight / 2d)) / oldExtentHeight
            : 0.5d;

        var settings = App.Settings.Current;
        var zoom = settings.ReaderZoomMode == "custom"
            ? Math.Clamp(settings.ReaderZoomFactor, 0.6d, 2d)
            : 1d;
        var fitScale = Math.Min(availableWidth / logicalWidth, availableHeight / logicalHeight);
        var frameWidth = Math.Max(1d, logicalWidth * fitScale * zoom);
        var frameHeight = Math.Max(1d, logicalHeight * fitScale * zoom);

        ReaderFrameViewbox.Width = frameWidth;
        ReaderFrameViewbox.Height = frameHeight;
        ReaderFrameCanvas.Width = Math.Max(availableWidth, frameWidth);
        ReaderFrameCanvas.Height = Math.Max(availableHeight, frameHeight);
        ReaderProgressStrip.MaxWidth = availableWidth;

        var revision = ++_readerFrameGeometryRevision;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (revision != _readerFrameGeometryRevision) return;

            var horizontalOffset = Math.Clamp(
                (centerX * ReaderFrameViewport.ExtentWidth) - (ReaderFrameViewport.ViewportWidth / 2d),
                0d,
                Math.Max(0d, ReaderFrameViewport.ExtentWidth - ReaderFrameViewport.ViewportWidth));
            var verticalOffset = Math.Clamp(
                (centerY * ReaderFrameViewport.ExtentHeight) - (ReaderFrameViewport.ViewportHeight / 2d),
                0d,
                Math.Max(0d, ReaderFrameViewport.ExtentHeight - ReaderFrameViewport.ViewportHeight));
            ReaderFrameViewport.ChangeView(horizontalOffset, verticalOffset, null, true);
        });
    }

    private void ReaderReadingArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyFigmaReaderPageGeometry();
    }

    private void ApplyFigmaReaderSurfaceTheme()
    {
        var theme = EffectiveReaderTheme();
        var brush = theme switch
        {
            "dark" => new SolidColorBrush(ColorHelper.FromArgb(255, 31, 31, 31)),
            "sepia" => new SolidColorBrush(ColorHelper.FromArgb(255, 242, 232, 209)),
            _ => new SolidColorBrush(Colors.White)
        };
        ReaderSurface.Background = brush;
        ReaderLoadingLayer.Background = brush;
    }

    private async Task RefreshReaderPresentationAsync()
    {
        if (!_webReady) return;
        await ApplyWebReaderStyleAsync(_sectionFraction);
        ApplyFigmaReaderPageGeometry();
        await ApplyReaderViewEnhancementsAsync();
        await ApplyNoteOnlyHighlightsAsync();
    }
}
