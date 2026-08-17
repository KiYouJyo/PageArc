using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PageArc.Pages;

public sealed partial class ReaderPage
{
    private bool _figmaReaderControlsReady;
    private bool _figmaThemeHooked;

    private void ReaderPage_FigmaLoaded(object sender, RoutedEventArgs e)
    {
        // This handler is registered from XAML before ReaderPage_Loaded is subscribed in the
        // constructor, so the inherited app theme is synchronized before the WebView style is read.
        SyncFollowAppReaderTheme();
        InitializeFigmaReaderControls();
        ReaderPage_NotesLoaded(sender, e);

        if (!_figmaThemeHooked)
        {
            _figmaThemeHooked = true;
            ReaderRootGrid.ActualThemeChanged += ReaderRootGrid_ActualThemeChanged;
        }
    }

    private void InitializeFigmaReaderControls()
    {
        var settings = App.Settings.Current;
        var previousSettingsReady = _settingsReady;
        _settingsReady = false;
        _figmaReaderControlsReady = false;
        try
        {
            SelectByTag(ReaderThemeCombo, EffectiveReaderTheme());
            ReaderFontScaleSlider.Value = settings.FontScale;
            ReaderLineHeightSlider.Value = settings.LineHeight;
            ContinuousScrollToggle.IsOn = settings.ContinuousScrolling;
            SelectByTag(FigmaFontFamilyCombo, settings.DefaultFont);
            FigmaShowProgressToggle.IsOn = settings.ShowReadingProgress;
        }
        finally
        {
            _settingsReady = previousSettingsReady;
            _figmaReaderControlsReady = true;
        }

        BookmarkToolText.Text = ReaderText("书签", "しおり", "Bookmark");
        ReaderFontLabel.Text = ReaderText("字体", "フォント", "Font");
        ThemeLightText.Text = ReaderText("浅色", "ライト", "Light");
        ThemeSepiaText.Text = ReaderText("米黄色", "セピア", "Sepia");
        ThemeDarkText.Text = ReaderText("深色", "ダーク", "Dark");
        LineCompactText.Text = ReaderText("紧凑", "狭い", "Compact");
        LineNormalText.Text = ReaderText("标准", "標準", "Normal");
        LineRelaxedText.Text = ReaderText("宽松", "広い", "Relaxed");
        ReaderPageWidthLabel.Text = ReaderText("页面宽度", "ページ幅", "Page width");
        WidthNarrowText.Text = ReaderText("窄", "狭い", "Narrow");
        WidthMediumText.Text = ReaderText("中", "中", "Medium");
        WidthWideText.Text = ReaderText("宽", "広い", "Wide");
        ReaderBehaviorLabel.Text = ReaderText("阅读方式", "読書方式", "Reading mode");
        ContinuousScrollLabel.Text = ReaderText("连续滚动", "連続スクロール", "Continuous scrolling");
        ShowProgressLabel.Text = ReaderText("显示阅读进度", "読書進捗を表示", "Show reading progress");
        FigmaResetReaderButton.Content = ReaderText("恢复默认设置", "既定の設定に戻す", "Restore defaults");

        UpdateFigmaReaderSelectionVisuals();
        ApplyFigmaReaderPageGeometry();
        ApplyFigmaReaderSurfaceTheme();
        ApplyProgressVisibility();
    }

    private async void ReaderRootGrid_ActualThemeChanged(FrameworkElement sender, object args)
    {
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
        if (_webReady) await ApplyWebReaderStyleAsync(_sectionFraction);
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
        if (_webReady) await ApplyWebReaderStyleAsync(_sectionFraction);
    }

    private async void FigmaFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_figmaReaderControlsReady || FigmaFontFamilyCombo.SelectedItem is not ComboBoxItem { Tag: string font }) return;
        App.Settings.Update(settings => settings.DefaultFont = font);
        if (_webReady) await ApplyWebReaderStyleAsync(_sectionFraction);
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
        if (_webReady) await ApplyWebReaderStyleAsync(_sectionFraction);
    }

    private async void ReaderPageWidth_Click(object sender, RoutedEventArgs e)
    {
        if (!_figmaReaderControlsReady || sender is not Button { Tag: string width }) return;
        width = width is "narrow" or "wide" ? width : "medium";
        App.Settings.Update(settings => settings.PageWidth = width);
        UpdateFigmaReaderSelectionVisuals();
        ApplyFigmaReaderPageGeometry();
        if (_webReady) await ApplyWebReaderStyleAsync(_sectionFraction);
    }

    private void FigmaShowProgress_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_figmaReaderControlsReady) return;
        App.Settings.Update(settings => settings.ShowReadingProgress = FigmaShowProgressToggle.IsOn);
        ApplyProgressVisibility();
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
                settings.ContinuousScrolling = false;
                settings.ShowReadingProgress = true;
            });

            SelectByTag(ReaderThemeCombo, EffectiveReaderTheme());
            SelectByTag(FigmaFontFamilyCombo, "book");
            ReaderFontScaleSlider.Value = 1.0;
            ReaderLineHeightSlider.Value = 1.75;
            ContinuousScrollToggle.IsOn = false;
            FigmaShowProgressToggle.IsOn = true;
        }
        finally
        {
            _settingsReady = previousSettingsReady;
            _figmaReaderControlsReady = true;
        }

        UpdateFigmaReaderSelectionVisuals();
        ApplyFigmaReaderPageGeometry();
        ApplyFigmaReaderSurfaceTheme();
        ApplyProgressVisibility();
        if (_webReady) await ApplyWebReaderStyleAsync(_sectionFraction);
    }

    private void ReaderSettingsClose_Click(object sender, RoutedEventArgs e) => AppearanceButton.Flyout?.Hide();

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

        var width = App.Settings.Current.PageWidth;
        SetSegmentSelection(WidthNarrowButton, width == "narrow");
        SetSegmentSelection(WidthMediumButton, width is not ("narrow" or "wide"));
        SetSegmentSelection(WidthWideButton, width == "wide");
    }

    private static void SetThemeCardSelection(Button button, bool selected)
    {
        button.BorderThickness = new Thickness(selected ? 2 : 1);
        button.BorderBrush = selected
            ? new SolidColorBrush(ColorHelper.FromArgb(255, 0, 95, 184))
            : new SolidColorBrush(ColorHelper.FromArgb(72, 117, 117, 117));
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
        var pageWidth = App.Settings.Current.PageWidth switch
        {
            "narrow" => 640d,
            "wide" => 900d,
            _ => 760d
        };
        ReaderSurface.MaxWidth = pageWidth;
        ReaderProgressStrip.MaxWidth = pageWidth;

        var navOffset = pageWidth / 2d + 46d;
        PreviousPageButton.RenderTransform = new TranslateTransform { X = -navOffset };
        NextPageButton.RenderTransform = new TranslateTransform { X = navOffset };
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
}
