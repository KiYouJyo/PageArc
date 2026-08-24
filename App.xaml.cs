using System.Collections.Concurrent;
using Microsoft.UI.Xaml;
using PageArc.Models;
using PageArc.Services;

namespace PageArc;

public partial class App : Application
{
    private readonly WindowsAppLifecycleService _lifecycle = new();
    private readonly SemaphoreSlim _activationGate = new(1, 1);
    private readonly ConcurrentQueue<AppActivationRequest> _queuedActivations = new();

    public static MainWindow? MainWindow { get; private set; }
    public static SettingsService Settings { get; } = new();
    public static LocalizationService Localization { get; } = new(Settings);
    public static LibraryService Library { get; } = new();
    public static ImportFolderService ImportFolders { get; } = new(Library);
    public static CategoryService Categories { get; } = new();
    public static ReadingDataService ReadingData { get; } = new();
    public static GitHubUpdateService Updates { get; } = new();
    public static JumpListService JumpLists { get; } = new();

    internal static string PendingNavigationTag { get; set; } = "library";

    public App()
    {
        StartupDiagnostics.Reset();
        StartupDiagnostics.Log("App constructor entered.");
        UnhandledException += (_, e) => StartupDiagnostics.Log("Application.UnhandledException", e.Exception);
        _lifecycle.ActivationReceived += Lifecycle_ActivationReceived;
        try
        {
            // Apply the persisted override before the first XAML resource context is
            // created. Applying it in OnLaunched leaves x:Uid values stuck in English.
            Settings.Load();
            Localization.ApplyPersistedLanguage(Settings.Current);
            StartupDiagnostics.Log("Settings and localization initialized before XAML.");
            InitializeComponent();
            StartupDiagnostics.Log("App.InitializeComponent completed.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("App.InitializeComponent failed", ex);
            throw;
        }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartupDiagnostics.Log("App.OnLaunched entered.");
        try
        {
            var lifecycle = await _lifecycle.RegisterAsync();
            if (!lifecycle.IsPrimaryInstance)
            {
                StartupDiagnostics.Log("Activation redirected to the existing PageArc instance; shutting down secondary instance.");
                Exit();
                return;
            }

            Library.DuplicateDetectionEnabled = Settings.Current.DuplicateDetection;
            Library.Load();
            StartupDiagnostics.Log("Library.Load completed.");
            var repairedCovers = await Library.EnsureImportedCoversAsync();
            StartupDiagnostics.Log($"Library cover cache ready; repaired={repairedCovers}.");
            ImportFolders.Load();
            StartupDiagnostics.Log("ImportFolders.Load completed.");
            Categories.Load(Library.Books);
            StartupDiagnostics.Log("Categories.Load completed.");
            ReadingData.Load();
            StartupDiagnostics.Log("ReadingData.Load completed.");
            CreateMainWindow();
            StartupDiagnostics.Log("CreateMainWindow completed.");

            await HandleActivationAsync(lifecycle.InitialRequest);
            while (_queuedActivations.TryDequeue(out var queued))
                await HandleActivationAsync(queued);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("App.OnLaunched failed", ex);
            throw;
        }
    }

    private void Lifecycle_ActivationReceived(object? sender, AppActivationRequest request)
    {
        var window = MainWindow;
        if (window is null)
        {
            _queuedActivations.Enqueue(request);
            return;
        }

        window.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                window.Activate();
                await HandleActivationAsync(request);
            }
            catch (Exception ex)
            {
                StartupDiagnostics.Log("Redirected activation handling failed.", ex);
            }
        });
    }

    private async Task HandleActivationAsync(AppActivationRequest request)
    {
        if (request.Kind == AppActivationRequestKind.Launch) return;
        await _activationGate.WaitAsync();
        try
        {
            switch (request.Kind)
            {
                case AppActivationRequestKind.Files:
                    await OpenActivatedFilesAsync(request.FilePaths);
                    break;
                case AppActivationRequestKind.Book:
                    OpenActivatedBook(request.BookId);
                    break;
                case AppActivationRequestKind.Protocol:
                    MainWindow?.NavigateTo("library");
                    MainWindow?.Activate();
                    break;
            }
        }
        finally
        {
            _activationGate.Release();
        }
    }

    private static async Task OpenActivatedFilesAsync(IReadOnlyList<string> paths)
    {
        BookEntry? first = null;
        foreach (var path in paths)
        {
            var result = await Library.ImportDetailedAsync(path);
            if (first is null && result.Book is not null
                && result.Disposition is LibraryImportDisposition.Added or LibraryImportDisposition.ExistingPath or LibraryImportDisposition.DuplicateContent)
            {
                first = result.Book;
            }
        }

        if (first is null) return;
        Categories.Load(Library.Books);
        MainWindow?.Activate();
        MainWindow?.OpenBook(first);
    }

    private static void OpenActivatedBook(string? bookId)
    {
        var book = Library.FindById(bookId);
        if (book is null || book.IsMissing || !File.Exists(book.FilePath))
        {
            MainWindow?.NavigateTo("library");
            MainWindow?.Activate();
            return;
        }
        MainWindow?.Activate();
        MainWindow?.OpenBook(book);
    }

    private static void CreateMainWindow()
    {
        StartupDiagnostics.Log("Creating MainWindow.");
        MainWindow = new MainWindow();
        Updates.InitializeForWindow(MainWindow);
        StartupDiagnostics.Log("MainWindow constructed; activating.");
        MainWindow.Activate();
        StartupDiagnostics.Log("MainWindow activated.");
    }

    public static void ReloadMainWindow(string navigationTag = "settings")
    {
        PendingNavigationTag = navigationTag;
        var previous = MainWindow;
        previous?.SaveWindowPlacement();
        CreateMainWindow();
        previous?.Close();
    }
}
