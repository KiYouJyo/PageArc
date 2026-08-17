namespace PageArc.Models;

public enum ShellTabKind
{
    Home,
    Reader
}

public sealed record ShellTabSession(string Id, ShellTabKind Kind, string? BookId = null);

public sealed class ShellTabSessionManager
{
    private readonly List<ShellTabSession> _tabs = [];

    public IReadOnlyList<ShellTabSession> Tabs => _tabs;

    public ShellTabSession CreateHome()
    {
        var session = new ShellTabSession(Guid.NewGuid().ToString("N"), ShellTabKind.Home);
        _tabs.Add(session);
        return session;
    }

    public (ShellTabSession Session, bool Created) OpenReader(string bookId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        var existing = _tabs.FirstOrDefault(tab =>
            tab.Kind == ShellTabKind.Reader && string.Equals(tab.BookId, bookId, StringComparison.Ordinal));
        if (existing is not null) return (existing, false);

        var session = new ShellTabSession(Guid.NewGuid().ToString("N"), ShellTabKind.Reader, bookId);
        _tabs.Add(session);
        return (session, true);
    }

    public bool Close(string id)
    {
        var index = _tabs.FindIndex(tab => string.Equals(tab.Id, id, StringComparison.Ordinal));
        if (index < 0) return false;
        _tabs.RemoveAt(index);
        return true;
    }

    public ShellTabSession EnsureHomeIfEmpty()
    {
        if (_tabs.Count > 0) return _tabs[0];
        return CreateHome();
    }

    public ShellTabSession? Find(string id) =>
        _tabs.FirstOrDefault(tab => string.Equals(tab.Id, id, StringComparison.Ordinal));
}
