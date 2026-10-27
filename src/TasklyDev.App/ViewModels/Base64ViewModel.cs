using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using TasklyDev.Core.Encoding;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

public sealed class Base64ViewModel : INotifyPropertyChanged
{
    private readonly IBase64Service _service;
    private readonly IClipboardService _clipboard;
    private readonly RelayCommand _encodeCommand;
    private readonly RelayCommand _decodeCommand;
    private readonly RelayCommand _runCommand;
    private string _inputText = string.Empty;
    private string _outputText = string.Empty;
    private string _statusMessage = "Enter text or Base64, then choose an operation.";
    private bool _isError;
    private bool _isEncoding = true;
    private bool _includePadding = true;
    private Base64Variant _selectedVariant;

    public Base64ViewModel(IBase64Service service, IClipboardService clipboard)
    {
        _service = service;
        _clipboard = clipboard;
        _encodeCommand = new RelayCommand(_ => Encode());
        _decodeCommand = new RelayCommand(_ => Decode());
        _runCommand = new RelayCommand(_ =>
        {
            if (IsEncoding)
            {
                Encode();
            }
            else
            {
                Decode();
            }
        });
        EncodeCommand = _encodeCommand;
        DecodeCommand = _decodeCommand;
        RunCommand = _runCommand;
        CopyOutputCommand = new RelayCommand(_ => CopyOutput());
        SwapCommand = new RelayCommand(_ => Swap());
        ClearCommand = new RelayCommand(_ => Clear());
        LoadSampleCommand = new RelayCommand(_ => LoadSample());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand EncodeCommand { get; }
    public ICommand DecodeCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand CopyOutputCommand { get; }
    public ICommand SwapCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand LoadSampleCommand { get; }

    public Array Variants => Enum.GetValues<Base64Variant>();

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
            OnPropertyChanged(nameof(InputCharacterCount));
            OnPropertyChanged(nameof(InputByteCount));
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
            OnPropertyChanged(nameof(OutputCharacterCount));
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

    public bool IsEncoding
    {
        get => _isEncoding;
        set
        {
            if (_isEncoding == value)
            {
                return;
            }

            _isEncoding = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDecoding));
            OnPropertyChanged(nameof(InputLabel));
            OnPropertyChanged(nameof(OutputLabel));
            OnPropertyChanged(nameof(PrimaryActionLabel));
        }
    }

    public bool IncludePadding
    {
        get => _includePadding;
        set
        {
            if (_includePadding == value)
            {
                return;
            }

            _includePadding = value;
            OnPropertyChanged();
        }
    }

    public bool IsDecoding
    {
        get => !IsEncoding;
        set => IsEncoding = !value;
    }

    public Base64Variant SelectedVariant
    {
        get => _selectedVariant;
        set
        {
            if (_selectedVariant == value)
            {
                return;
            }

            _selectedVariant = value;
            OnPropertyChanged();
        }
    }

    public string InputLabel => IsEncoding ? "Input (UTF-8 text)" : "Input (Base64)";
    public string OutputLabel => IsEncoding ? "Output (Base64)" : "Output (UTF-8 text)";
    public string PrimaryActionLabel => IsEncoding ? "Encode" : "Decode";
    public int InputCharacterCount => InputText.Length;
    public int InputByteCount => Encoding.UTF8.GetByteCount(InputText);
    public int OutputCharacterCount => OutputText.Length;

    private void Encode()
    {
        try
        {
            OutputText = _service.EncodeUtf8(InputText, SelectedVariant, IncludePadding);
            SetStatus("Text encoded successfully. Your input remains in memory only.", isError: false);
        }
        catch (EncoderFallbackException)
        {
            OutputText = string.Empty;
            SetStatus("Input contains text that cannot be represented as valid UTF-8.", isError: true);
        }
    }

    private void Decode()
    {
        try
        {
            OutputText = _service.DecodeUtf8(InputText, SelectedVariant);
            SetStatus("Base64 decoded successfully as UTF-8. Your input remains in memory only.", isError: false);
        }
        catch (FormatException)
        {
            OutputText = string.Empty;
            SetStatus("Input is not valid Base64 for the selected variant.", isError: true);
        }
        catch (DecoderFallbackException)
        {
            OutputText = string.Empty;
            SetStatus("Decoded bytes are not valid UTF-8 text.", isError: true);
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
            SetStatus("Output copied to the clipboard.", isError: false);
        }
        else
        {
            SetStatus("Clipboard is unavailable or busy. Try copying again.", isError: true);
        }
    }

    private void Swap()
    {
        InputText = OutputText;
        OutputText = string.Empty;
        IsEncoding = !IsEncoding;
        SetStatus("Output moved to input and operation direction switched.", isError: false);
    }

    private void Clear()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
        SetStatus("Input and output cleared.", isError: false);
    }

    private void LoadSample()
    {
        InputText = "Hello, TasklyDev! UTF-8 works: 你好 🌱";
        IsEncoding = true;
        SetStatus("Sample text loaded. Choose Encode to create its Base64 value.", isError: false);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
