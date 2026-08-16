using Microsoft.Windows.AppLifecycle;
using PageArc.Models;
using Windows.ApplicationModel.Activation;

namespace PageArc.Services;

public sealed record WindowsAppLifecycleRegistration(bool IsPrimaryInstance, AppActivationRequest InitialRequest);

public sealed class WindowsAppLifecycleService : IDisposable
{
    public const string MainInstanceKey = "PageArc.Main";
    private AppInstance? _currentInstance;
    private bool _subscribed;

    public event EventHandler<AppActivationRequest>? ActivationReceived;

    public async Task<WindowsAppLifecycleRegistration> RegisterAsync()
    {
        AppActivationArguments? activationArgs = null;
        try
        {
            _currentInstance = AppInstance.GetCurrent();
            activationArgs = _currentInstance.GetActivatedEventArgs();
            var primary = AppInstance.FindOrRegisterForKey(MainInstanceKey);
            if (!primary.IsCurrent)
            {
                await primary.RedirectActivationToAsync(activationArgs);
                return new WindowsAppLifecycleRegistration(false, AppActivationRequest.Launch());
            }

            _currentInstance.Activated += CurrentInstance_Activated;
            _subscribed = true;
            return new WindowsAppLifecycleRegistration(true, Parse(activationArgs));
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Windows App Lifecycle registration unavailable; continuing with local launch activation.", ex);
            var raw = string.Join(" ", Environment.GetCommandLineArgs().Skip(1).Select(QuoteIfNeeded));
            return new WindowsAppLifecycleRegistration(true, AppActivationRequestParser.FromLaunchArguments(raw));
        }
    }

    private void CurrentInstance_Activated(object? sender, AppActivationArguments args)
    {
        try
        {
            ActivationReceived?.Invoke(this, Parse(args));
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Redirected Windows activation could not be parsed.", ex);
        }
    }

    public static AppActivationRequest Parse(AppActivationArguments args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Kind switch
        {
            ExtendedActivationKind.File when args.Data is IFileActivatedEventArgs fileArgs =>
                AppActivationRequestParser.FromFilePaths(fileArgs.Files.Select(item => item.Path)),
            ExtendedActivationKind.Protocol when args.Data is IProtocolActivatedEventArgs protocolArgs =>
                AppActivationRequestParser.FromProtocol(protocolArgs.Uri),
            ExtendedActivationKind.Launch when args.Data is ILaunchActivatedEventArgs launchArgs =>
                AppActivationRequestParser.FromLaunchArguments(launchArgs.Arguments),
            _ => AppActivationRequest.Launch()
        };
    }

    public void Dispose()
    {
        if (_subscribed && _currentInstance is not null)
        {
            _currentInstance.Activated -= CurrentInstance_Activated;
            _subscribed = false;
        }
    }

    private static string QuoteIfNeeded(string value) =>
        value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
}
