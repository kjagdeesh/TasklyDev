using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TasklyDev.Core.Conversion;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

/// <summary>Coordinates number-base selection, integer conversion, and output copying.</summary>
public sealed class NumberBaseViewModel : INotifyPropertyChanged
{
    private readonly INumberBaseService _service;
    private readonly IClipboardService _clipboard;
    private int _sourceBase = 10;
    private int _targetBase = 16;
    private string _inputText = string.Empty;
    private string _outputText = string.Empty;
    private string _statusMessage = "Enter an integer and select its source and target bases.";
    private bool _isError;

    /// <summary>Creates the number-base converter with conversion and clipboard services.</summary>
    public NumberBaseViewModel(INumberBaseService service, IClipboardService clipboard)
    {
        _service = service;
        _clipboard = clipboard;
        Bases = new ObservableCollection<int>(Enumerable.Range(2, 35));
        ConvertCommand = new RelayCommand(_ => ConvertValue());
        SwapBasesCommand = new RelayCommand(_ => SwapBases());
        CopyOutputCommand = new RelayCommand(_ => CopyOutput());
        ClearCommand = new RelayCommand(_ => Clear());
        LoadSampleCommand = new RelayCommand(_ => LoadSample());
        ApplyPresetCommand = new RelayCommand(parameter =>
        {
            if (parameter is string preset)
            {
                ApplyPreset(preset);
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<int> Bases { get; }
    public ICommand ConvertCommand { get; }
    public ICommand SwapBasesCommand { get; }
    public ICommand CopyOutputCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand LoadSampleCommand { get; }
    public ICommand ApplyPresetCommand { get; }

    public int SourceBase
    {
        get => _sourceBase;
        set
        {
            if (_sourceBase == value)
            {
                return;
            }

            _sourceBase = value;
            OnPropertyChanged();
        }
    }

    public int TargetBase
    {
        get => _targetBase;
        set
        {
            if (_targetBase == value)
            {
                return;
            }

            _targetBase = value;
            OnPropertyChanged();
        }
    }

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

    public string OutputText
    {
        get => _outputText;
        private set
        {
            if (_outputText == value)
            {
                return;
            }

            _outputText = value;
            OnPropertyChanged();
        }
    }

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

    private void ConvertValue()
    {
        try
        {
            OutputText = _service.Convert(InputText, SourceBase, TargetBase);
            SetStatus($"Converted from base {SourceBase} to base {TargetBase}.", isError: false);
        }
        catch (FormatException exception)
        {
            OutputText = string.Empty;
            SetStatus(exception.Message, isError: true);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            OutputText = string.Empty;
            SetStatus(exception.Message, isError: true);
        }
    }

    private void ApplyPreset(string preset)
    {
        (SourceBase, TargetBase) = preset switch
        {
            "DEC_HEX" => (10, 16),
            "HEX_DEC" => (16, 10),
            "DEC_BIN" => (10, 2),
            "BIN_HEX" => (2, 16),
            "OCT_DEC" => (8, 10),
            _ => (SourceBase, TargetBase)
        };

        if (InputText.Length > 0)
        {
            ConvertValue();
        }
    }

    private void SwapBases()
    {
        var previousOutput = OutputText;
        (SourceBase, TargetBase) = (TargetBase, SourceBase);
        if (previousOutput.Length > 0)
        {
            InputText = previousOutput;
            ConvertValue();
        }
        else if (InputText.Length > 0)
        {
            ConvertValue();
        }
    }

    private void CopyOutput()
    {
        if (OutputText.Length == 0)
        {
            SetStatus("There is no output to copy.", isError: true);
            return;
        }

        if (_clipboard.TrySetText(OutputText))
        {
            SetStatus("Converted value copied to the clipboard.", isError: false);
        }
        else
        {
            SetStatus("Clipboard is unavailable or busy. Try copying again.", isError: true);
        }
    }

    private void Clear()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
        SetStatus("Input and output cleared.", isError: false);
    }

    private void LoadSample()
    {
        SourceBase = 10;
        TargetBase = 16;
        InputText = "984521093120";
        ConvertValue();
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
