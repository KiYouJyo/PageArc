using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using PageArc.Models;
using PageArc.Pages;
using PageArc.Services;

namespace PageArc;

public sealed partial class MainWindow
{
    private readonly ShellSessionStore _shellSessionStore = new();
    private Microsoft.UI.Xaml.DispatcherTimer? _shellSessionTimer;
    private bool _shellSessionRestoreAttempted;

    private void MainWindow_SessionRestoreLoaded(object sender, RoutedEventArgs e)
    {
        if (_shellSessionRestoreAttempted) return;
        _shellSessionRestoreAttempted = true;

        try
        {
            var state = _shellSessionStore.Load();
            if (state.Tabs.Count > 0) RestoreShellSession(state);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Shell session restore failed; using a fresh Home tab.", ex);
        }

        Activated += MainWindow_SessionActivated;
        Closed += MainWindow_SessionClosed;
        _shellSessionTimer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _shellSessionTimer.Tick += ShellSessionTimer_Tick;
        _shellSessionTimer.Start();
    }

    private void RestoreShellSession(ShellSessionState state)
    {
        foreach (var frame in _readerFrames.Values)
            if (frame.Content is ReaderPage reader) reader.PrepareForClose();
        _readerFrames.Clear();
        ReaderHost.Children.Clear();
        _tabItems.Clear();
        ShellTabItems.Children.Clear();

        var restored = new List<ShellTabSession>();
        var lastOpenedBeforeRestore = new Dictionary<string, DateTimeOffset?>(StringComparer.Ordinal);
        foreach (var session in state.Tabs)
        {
            if (session.Kind == ShellTabKind.Home)
            {
                restored.Add(session);
                _tabItems[session.Id] = CreateTabVisual(session.Id, HomeTabTitle(), Symbol.Home, 220);
                continue;
            }

            if (string.IsNullOrWhiteSpace(session.BookId)) continue;
            var book = App.Library.Books.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, session.BookId, StringComparison.Ordinal)
                && !candidate.IsMissing
                && File.Exists(candidate.FilePath));
            if (book is null) continue;

            lastOpenedBeforeRestore[book.Id] = book.LastOpenedAt;
            var frame = new Frame { Visibility = Visibility.Collapsed };
            if (!frame.Navigate(typeof(ReaderPage), book, new SuppressNavigationTransitionInfo())) continue;
            var title = string.IsNullOrWhiteSpace(book.Title) ? Path.GetFileNameWithoutExtension(book.FilePath) : book.Title;
            _readerFrames[session.Id] = frame;
            _tabItems[session.Id] = CreateTabVisual(session.Id, title, Symbol.Library, 300);
            ReaderHost.Children.Add(frame);
            restored.Add(session);
        }

        _tabSessions.ReplaceAll(restored);
        if (_tabSessions.Tabs.Count == 0)
        {
            CreateHomeTab(select: true);
            return;
        }

        var selectedId = state.SelectedTabId;
        if (string.IsNullOrWhiteSpace(selectedId) || _tabSessions.Find(selectedId) is null)
            selectedId = _tabSessions.Tabs[0].Id;
        SelectTab(selectedId);
        DispatcherQueue.TryEnqueue(() =>
        {
            foreach (var pair in lastOpenedBeforeRestore)
            {
                var book = App.Library.Books.FirstOrDefault(candidate => string.Equals(candidate.Id, pair.Key, StringComparison.Ordinal));
                if (book is not null) book.LastOpenedAt = pair.Value;
            }
            if (lastOpenedBeforeRestore.Count > 0) App.Library.Save();
        });
        StartupDiagnostics.Log($"Restored {_tabSessions.Tabs.Count} shell tab(s); selected={selectedId}.");
    }

    private void MainWindow_SessionActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated) SaveShellSession();
    }

    private void ShellSessionTimer_Tick(object? sender, object e) => SaveShellSession();

    private void MainWindow_SessionClosed(object sender, WindowEventArgs args)
    {
        SaveShellSession();
        if (_shellSessionTimer is not null)
        {
            _shellSessionTimer.Stop();
            _shellSessionTimer.Tick -= ShellSessionTimer_Tick;
            _shellSessionTimer = null;
        }
        Activated -= MainWindow_SessionActivated;
        Closed -= MainWindow_SessionClosed;
    }

    private void SaveShellSession()
    {
        if (!_shellSessionRestoreAttempted) return;
        try
        {
            _shellSessionStore.Save(new ShellSessionState
            {
                SelectedTabId = _selectedTabId,
                Tabs = _tabSessions.Tabs.ToList()
            });
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Shell session persistence failed.", ex);
        }
    }
}
