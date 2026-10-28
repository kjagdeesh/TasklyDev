using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using TasklyDev.Core.Encoding;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

/// <summary>Coordinates URL transformation modes, input/output, and clipboard actions.</summary>
public sealed class UrlCodecViewModel : INotifyPropertyChanged
{
    private readonly IUrlCodecService _service;
    private readonly IClipboardService _clipboard;
    private string _inputText = string.Empty;
    private string _outputText = string.Empty;
    private string _statusMessage = "Choose a mode and operation, then enter URL text or a value.";
    private bool _isError;
    private bool _isEncoding = true;
    private UrlEncodingMode _selectedMode = UrlEncodingMode.FullUrl;
    private UrlCharacterEncoding _selectedCharacterEncoding = UrlCharacterEncoding.Utf8;
    private UrlLineEnding _destinationLineEnding = UrlLineEnding.Lf;
    private bool _decodeEachLine;
    private bool _recursiveDecode;

    /// <summary>Creates the URL codec view model with transformation and clipboard services.</summary>
    public UrlCodecViewModel(IUrlCodecService service, IClipboardService clipboard)
    {
        _service = service;
        _clipboard = clipboard;
        RunCommand = new RelayCommand(_ => Run());
        EncodeCommand = new RelayCommand(_ => Transform(encode: true));
        DecodeCommand = new RelayCommand(_ => Transform(encode: false));
        CopyOutputCommand = new RelayCommand(_ => CopyOutput());
        SwapCommand = new RelayCommand(_ => Swap());
        ClearCommand = new RelayCommand(_ => Clear());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the available URL encoding modes.</summary>
    public Array Modes => Enum.GetValues<UrlEncodingMode>();

    /// <summary>Gets the character encodings available for percent encoding and decoding.</summary>
    public Array CharacterEncodings => Enum.GetValues<UrlCharacterEncoding>();

    /// <summary>Gets the available destination line separators.</summary>
    public Array LineEndings => Enum.GetValues<UrlLineEnding>();

    /// <summary>Gets or sets the input value.</summary>
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

    /// <summary>Gets the last transformation output.</summary>
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

    /// <summary>Gets the status message shown to the user.</summary>
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

    /// <summary>Gets whether the latest operation reported an error.</summary>
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

    /// <summary>Gets or sets whether the active operation is encoding.</summary>
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

    /// <summary>Gets or sets the inverse of <see cref="IsEncoding"/> for the decode selector.</summary>
    public bool IsDecoding
    {
        get => !IsEncoding;
        set => IsEncoding = !value;
    }

    /// <summary>Gets or sets the selected URL processing mode.</summary>
    public UrlEncodingMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (_selectedMode == value)
            {
                return;
            }

            _selectedMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModeDescription));
        }
    }

    /// <summary>Gets or sets the character encoding used for percent-escaped bytes.</summary>
    public UrlCharacterEncoding SelectedCharacterEncoding
    {
        get => _selectedCharacterEncoding;
        set
        {
            if (_selectedCharacterEncoding == value)
            {
                return;
            }

            _selectedCharacterEncoding = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Gets or sets the destination line separator.</summary>
    public UrlLineEnding DestinationLineEnding
    {
        get => _destinationLineEnding;
        set
        {
            if (_destinationLineEnding == value)
            {
                return;
            }

            _destinationLineEnding = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Gets or sets whether decoding treats each input line as a separate value.</summary>
    public bool DecodeEachLine
    {
        get => _decodeEachLine;
        set
        {
            if (_decodeEachLine == value)
            {
                return;
            }

            _decodeEachLine = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Gets or sets whether decoding repeats until stable or the 16-pass limit is reached.</summary>
    public bool RecursiveDecode
    {
        get => _recursiveDecode;
        set
        {
            if (_recursiveDecode == value)
            {
                return;
            }

            _recursiveDecode = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Gets a short explanation of the selected mode's escaping rules.</summary>
    public string ModeDescription => SelectedMode switch
    {
        UrlEncodingMode.FullUrl => "Processes a regular or whole-URL percent-encoded absolute URL. Decoding a regular URL preserves escaped reserved characters.",
        UrlEncodingMode.UrlComponent => "Encodes one value using the selected character encoding; a literal plus sign stays a plus when decoding.",
        UrlEncodingMode.QueryValue => "Encodes one query value. Spaces become + and a literal plus is encoded as %2B.",
        _ => string.Empty
    };

    /// <summary>Gets the input label for the selected operation.</summary>
    public string InputLabel => IsEncoding ? "Value to encode" : "Value to decode";

    /// <summary>Gets the output label for the selected operation.</summary>
    public string OutputLabel => IsEncoding ? "Encoded output" : "Decoded output";

    /// <summary>Gets the primary action label for the selected operation.</summary>
    public string PrimaryActionLabel => IsEncoding ? "Encode" : "Decode";

    /// <summary>Gets the UTF-16 character count for the input.</summary>
    public int InputCharacterCount => InputText.Length;

    /// <summary>Gets the UTF-8 byte count for the input.</summary>
    public int InputByteCount => Encoding.UTF8.GetByteCount(InputText);

    /// <summary>Gets the character count for the output.</summary>
    public int OutputCharacterCount => OutputText.Length;

    /// <summary>Gets parsed URL components when the latest operation used Full URL mode.</summary>
    public UrlComponents? Components { get; private set; }

    /// <summary>Gets whether parsed URL components are available for display.</summary>
    public bool HasComponents => Components is not null;

    /// <summary>Gets the command that performs the selected operation.</summary>
    public ICommand RunCommand { get; }

    /// <summary>Gets the command that explicitly encodes the input.</summary>
    public ICommand EncodeCommand { get; }

    /// <summary>Gets the command that explicitly decodes the input.</summary>
    public ICommand DecodeCommand { get; }

    /// <summary>Gets the command that copies the output value.</summary>
    public ICommand CopyOutputCommand { get; }

    /// <summary>Gets the command that moves output to input and reverses the operation.</summary>
    public ICommand SwapCommand { get; }

    /// <summary>Gets the command that clears input and output.</summary>
    public ICommand ClearCommand { get; }

    private void Run() => Transform(IsEncoding);

    private void Transform(bool encode)
    {
        try
        {
            var result = _service.Transform(
                InputText,
                SelectedMode,
                encode,
                SelectedCharacterEncoding,
                DestinationLineEnding,
                DecodeEachLine,
                RecursiveDecode);
            OutputText = result.Value;
            SetComponents(result.Components);
            SetStatus(
                encode ? "Value encoded successfully." : "Value decoded successfully.",
                isError: false);
        }
        catch (FormatException)
        {
            OutputText = string.Empty;
            SetComponents(null);
            SetStatus(
                SelectedMode == UrlEncodingMode.FullUrl
                    ? "Enter a valid absolute URL or percent-encoded absolute URL; malformed percent escapes and invalid character bytes are rejected."
                    : "Input contains malformed percent escapes or bytes invalid in the selected character encoding.",
                isError: true);
        }
        catch (EncoderFallbackException)
        {
            OutputText = string.Empty;
            SetComponents(null);
            SetStatus("Input contains text that cannot be represented in the selected character encoding.", isError: true);
        }
    }

    private void CopyOutput()
    {
        if (OutputText.Length == 0)
        {
            SetStatus("There is no output to copy.", isError: true);
            return;
        }

        var copied = _clipboard.TrySetText(OutputText);
        SetStatus(
            copied ? "Output copied to the clipboard." : "Clipboard is unavailable or busy. Try copying again.",
            isError: !copied);
    }

    private void Swap()
    {
        InputText = OutputText;
        OutputText = string.Empty;
        SetComponents(null);
        IsEncoding = !IsEncoding;
        SetStatus("Output moved to input and operation direction switched.", isError: false);
    }

    private void Clear()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
        SetComponents(null);
        SetStatus("Input and output cleared.", isError: false);
    }

    private void SetComponents(UrlComponents? components)
    {
        Components = components;
        OnPropertyChanged(nameof(Components));
        OnPropertyChanged(nameof(HasComponents));
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
