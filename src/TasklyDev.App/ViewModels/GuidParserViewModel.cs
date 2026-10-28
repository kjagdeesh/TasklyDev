using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TasklyDev.Core.Identifiers;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

/// <summary>Coordinates GUID/UUID validation, result presentation, and copy actions.</summary>
public sealed class GuidParserViewModel : INotifyPropertyChanged
{
    private readonly IGuidParserService _service;
    private readonly IClipboardService _clipboard;
    private string _inputText = string.Empty;
    private GuidParseResult? _result;
    private string _statusMessage = "Enter a GUID or UUID to inspect its format and canonical value.";
    private bool _isError;

    /// <summary>Creates the parser view model with parsing and clipboard services.</summary>
    public GuidParserViewModel(IGuidParserService service, IClipboardService clipboard)
    {
        _service = service;
        _clipboard = clipboard;
        ParseCommand = new RelayCommand(_ => Parse());
        CopyCanonicalCommand = new RelayCommand(_ => CopyCanonical());
        ClearCommand = new RelayCommand(_ => Clear());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the bound input text.</summary>
    public string InputText
    {
        get => _inputText;
        set
        {
            if (_inputText == value)
            {
                return;
            }

            _inputText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Gets the last parse result, if parsing has been attempted.</summary>
    public GuidParseResult? Result => _result;

    /// <summary>Gets the canonical lowercase D-format value for valid input.</summary>
    public string CanonicalValue => _result?.CanonicalValue ?? string.Empty;

    /// <summary>Gets the detected input format or an empty string when unavailable.</summary>
    public string DetectedFormat => _result?.InputFormat?.ToString() ?? string.Empty;

    /// <summary>Gets the UUID version description or an empty string before parsing.</summary>
    public string VersionDescription => _result?.VersionDescription ?? string.Empty;

    /// <summary>Gets whether a parse result is available.</summary>
    public bool HasResult => _result is not null;

    /// <summary>Gets whether the latest parse result is valid.</summary>
    public bool IsValid => _result?.IsValid == true;

    /// <summary>Gets whether the latest operation has reported an error.</summary>
    public bool IsError
    {
        get => _isError;
        private set
        {
            if (_isError == value)
            {
                return;
            }

            _isError = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Gets the user-facing status message.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Gets the command that parses and validates the input.</summary>
    public ICommand ParseCommand { get; }

    /// <summary>Gets the command that copies the canonical value.</summary>
    public ICommand CopyCanonicalCommand { get; }

    /// <summary>Gets the command that clears the input and result.</summary>
    public ICommand ClearCommand { get; }

    private void Parse()
    {
        _result = _service.Parse(InputText);
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(CanonicalValue));
        OnPropertyChanged(nameof(DetectedFormat));
        OnPropertyChanged(nameof(VersionDescription));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(IsValid));
        IsError = !_result.IsValid;
        StatusMessage = _result.Message;
    }

    private void CopyCanonical()
    {
        if (!IsValid || CanonicalValue.Length == 0)
        {
            IsError = true;
            StatusMessage = "Parse a valid GUID/UUID before copying its canonical value.";
            return;
        }

        var copied = _clipboard.TrySetText(CanonicalValue);
        IsError = !copied;
        StatusMessage = copied
            ? "Canonical GUID/UUID copied to the clipboard."
            : "Clipboard is unavailable or busy. Try copying again.";
    }

    private void Clear()
    {
        InputText = string.Empty;
        _result = null;
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(CanonicalValue));
        OnPropertyChanged(nameof(DetectedFormat));
        OnPropertyChanged(nameof(VersionDescription));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(IsValid));
        IsError = false;
        StatusMessage = "Enter a GUID or UUID to inspect its format and canonical value.";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
