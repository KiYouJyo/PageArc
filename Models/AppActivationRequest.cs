namespace PageArc.Models;

public enum AppActivationRequestKind
{
    Launch,
    Files,
    Book,
    Protocol
}

public sealed record AppActivationRequest(
    AppActivationRequestKind Kind,
    IReadOnlyList<string> FilePaths,
    string? BookId = null,
    Uri? Uri = null,
    string? RawArguments = null)
{
    public static AppActivationRequest Launch(string? arguments = null) =>
        new(AppActivationRequestKind.Launch, [], RawArguments: arguments);

    public static AppActivationRequest Files(IEnumerable<string> paths, string? arguments = null) =>
        new(AppActivationRequestKind.Files, paths.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray(), RawArguments: arguments);

    public static AppActivationRequest Book(string bookId, Uri? uri = null) =>
        new(AppActivationRequestKind.Book, [], bookId, uri);

    public static AppActivationRequest Protocol(Uri uri) =>
        new(AppActivationRequestKind.Protocol, [], Uri: uri);
}
