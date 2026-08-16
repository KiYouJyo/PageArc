using Microsoft.UI.Xaml;
using PageArc.Services;

namespace PageArc;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }
    public static SettingsService Settings { get; } = new();
    public static LocalizationService Localization { get; } = new(Settings);
    public static LibraryService Library { get; } = new();
    public static GitHubUpdateService Updates { get; } = new();

    internal static string PendingNavigationTag { get; set; } = "library";

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Settings.Load();
        Localization.ApplyPersistedLanguage(Settings.Current);
        Library.Load();
        CreateMainWindow();
    }

    private static void CreateMainWindow()
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    public static void ReloadMainWindow(string navigationTag = "settings")
    {
        PendingNavigationTag = navigationTag;
        var previous = MainWindow;
        CreateMainWindow();
        previous?.Close();
    }
}
