using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PageArc.Models;

public sealed class ConversionQueueItem : INotifyPropertyChanged
{
    private string _status = "Ready";
    private string? _outputPath;

    public required string FilePath { get; init; }
    public string FileName => Path.GetFileName(FilePath);
    public string Format => BookFormatRegistryProxy.NormalizeDisplayFormat(Path.GetExtension(FilePath));

    public string Status
    {
        get => _status;
        set
        {
            if (string.Equals(_status, value, StringComparison.Ordinal)) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public string? OutputPath
    {
        get => _outputPath;
        set
        {
            if (string.Equals(_outputPath, value, StringComparison.Ordinal)) return;
            _outputPath = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // Keep the model testable and independent from the service namespace while matching the
    // canonical PageArc format names shown by the conversion queue.
    private static class BookFormatRegistryProxy
    {
        public static string NormalizeDisplayFormat(string extension) => extension.TrimStart('.').ToUpperInvariant() switch
        {
            "AZW" => "MOBI",
            "KF8" => "AZW3",
            var value => value
        };
    }
}
