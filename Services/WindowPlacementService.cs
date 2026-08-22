using Microsoft.UI.Windowing;
using PageArc.Models;
using Windows.Graphics;

namespace PageArc.Services;

public sealed class WindowPlacementService : IDisposable
{
    private const int MinimumWidth = 720;
    private const int MinimumHeight = 540;
    private readonly AppWindow _window;
    private readonly SettingsService _settings;
    private RectInt32 _normalBounds;
    private bool _wasMaximized;
    private bool _restoring;

    public WindowPlacementService(AppWindow window, SettingsService settings)
    {
        _window = window;
        _settings = settings;
        _normalBounds = CurrentBounds();
    }

    public void Restore()
    {
        var saved = _settings.Current;
        _restoring = true;
        try
        {
            if (saved.HasWindowPlacement && saved.LastNormalWindowWidth > 0 && saved.LastNormalWindowHeight > 0)
            {
                var requested = new RectInt32(
                    saved.LastNormalWindowX,
                    saved.LastNormalWindowY,
                    saved.LastNormalWindowWidth,
                    saved.LastNormalWindowHeight);
                _normalBounds = ClampToVisibleWorkArea(requested);
                _window.MoveAndResize(_normalBounds);
                _wasMaximized = saved.WasWindowMaximized;
                if (_wasMaximized && _window.Presenter is OverlappedPresenter presenter)
                    presenter.Maximize();
            }
            else
            {
                _normalBounds = CurrentBounds();
            }
        }
        finally
        {
            _restoring = false;
            _window.Changed += Window_Changed;
        }
    }

    public void Save()
    {
        CaptureCurrentState();
        var bounds = _normalBounds;
        var maximized = _wasMaximized;
        _settings.Update(value =>
        {
            value.HasWindowPlacement = true;
            value.LastNormalWindowX = bounds.X;
            value.LastNormalWindowY = bounds.Y;
            value.LastNormalWindowWidth = bounds.Width;
            value.LastNormalWindowHeight = bounds.Height;
            value.WasWindowMaximized = maximized;
        });
    }

    public void Dispose() => _window.Changed -= Window_Changed;

    private void Window_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!_restoring) CaptureCurrentState();
    }

    private void CaptureCurrentState()
    {
        if (_window.Presenter is OverlappedPresenter presenter)
        {
            if (presenter.State == OverlappedPresenterState.Minimized) return;
            if (presenter.State == OverlappedPresenterState.Maximized)
            {
                _wasMaximized = true;
                return;
            }
        }

        _wasMaximized = false;
        _normalBounds = CurrentBounds();
    }

    private RectInt32 CurrentBounds() => new(
        _window.Position.X,
        _window.Position.Y,
        Math.Max(1, _window.Size.Width),
        Math.Max(1, _window.Size.Height));

    private static RectInt32 ClampToVisibleWorkArea(RectInt32 requested)
    {
        var area = DisplayArea.GetFromRect(requested, DisplayAreaFallback.Nearest);
        var work = area.WorkArea;
        var minimumWidth = Math.Min(MinimumWidth, work.Width);
        var minimumHeight = Math.Min(MinimumHeight, work.Height);
        var width = Math.Clamp(requested.Width, minimumWidth, work.Width);
        var height = Math.Clamp(requested.Height, minimumHeight, work.Height);
        var x = Math.Clamp(requested.X, work.X, work.X + work.Width - width);
        var y = Math.Clamp(requested.Y, work.Y, work.Y + work.Height - height);
        return new RectInt32(x, y, width, height);
    }
}
