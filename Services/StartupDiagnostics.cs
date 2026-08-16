namespace PageArc.Services;

public static class StartupDiagnostics
{
    private static readonly object Gate = new();

    public static string TempLogPath => Path.Combine(Path.GetTempPath(), "PageArc-startup.log");

    public static string LocalLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PageArc",
        "startup.log");

    public static void Reset()
    {
        TryDelete(TempLogPath);
        TryDelete(LocalLogPath);
        Log("Startup diagnostics initialized.");
    }

    public static void Log(string message)
    {
        var line = $"{DateTimeOffset.Now:O} [pid={Environment.ProcessId}] {message}{Environment.NewLine}";
        lock (Gate)
        {
            TryAppend(TempLogPath, line);
            TryAppend(LocalLogPath, line);
        }
    }

    public static void Log(string stage, Exception exception) =>
        Log($"{stage}: {exception.GetType().FullName}: {exception.Message}{Environment.NewLine}{exception}");

    private static void TryAppend(string path, string content)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.AppendAllText(path, content);
        }
        catch
        {
            // Diagnostics must never become a startup failure source.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }
}
