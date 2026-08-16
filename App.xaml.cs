using Microsoft.UI.Xaml;
using PageArc.Services;

namespace PageArc;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }
    public static SettingsService Settings { get; } = new();
    public static LocalizationService Localization { get; } = new(Settings);
    public static LibraryService Library { get; } = new();
    public static CategoryService Categories { get; } = new();
    public static GitHubUpdateService Updates { get; } = new();

    internal static string PendingNavigationTag { get; set; } = "library";

    public App()
    {
        StartupDiagnostics.Reset();
        StartupDiagnostics.Log("App constructor entered.");
        UnhandledException += (_, e) => StartupDiagnostics.Log("Application.UnhandledException", e.Exception);
        try
        {
            InitializeComponent();
            StartupDiagnostics.Log("App.InitializeComponent completed.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("App.InitializeComponent failed", ex);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartupDiagnostics.Log("App.OnLaunched entered.");
        try
        {
            Settings.Load();
            StartupDiagnostics.Log("Settings.Load completed.");
            Localization.ApplyPersistedLanguage(Settings.Current);
            StartupDiagnostics.Log("Localization.ApplyPersistedLanguage completed.");
            Library.Load();
            StartupDiagnostics.Log("Library.Load completed.");
            Categories.Load(Library.Books);
            StartupDiagnostics.Log("Categories.Load completed.");
            CreateMainWindow();
            StartupDiagnostics.Log("CreateMainWindow completed.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("App.OnLaunched failed", ex);
            throw;
        }
    }

    private static void CreateMainWindow()
    {
        StartupDiagnostics.Log("Creating MainWindow.");
        MainWindow = new MainWindow();
        StartupDiagnostics.Log("MainWindow constructed; activating.");
        MainWindow.Activate();
        StartupDiagnostics.Log("MainWindow activated.");
    }

    public static void ReloadMainWindow(string navigationTag = "settings")
    {
        PendingNavigationTag = navigationTag;
        var previous = MainWindow;
        CreateMainWindow();
        previous?.Close();
    }
}
