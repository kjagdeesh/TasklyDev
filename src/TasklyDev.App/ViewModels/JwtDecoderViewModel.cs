using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TasklyDev.Core.Tokens;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

/// <summary>Coordinates local JWT decoding and display of explicitly untrusted token data.</summary>
public sealed class JwtDecoderViewModel : INotifyPropertyChanged
{
    private readonly IJwtDecoderService _service;
    private readonly IJwtEncoderService _encoder;
    private readonly IClipboardService _clipboard;
    private string _tokenText = string.Empty;
    private JwtParseResult? _result;
    private string _statusMessage = "Paste a compact JWT to decode its header and payload locally.";
    private bool _isError;
    private bool _isEncoderMode;
    private string _encoderHeaderJson = "{\n  \"alg\": \"HS256\",\n  \"typ\": \"JWT\"\n}";
    private string _encoderPayloadJson = "{\n  \"sub\": \"1234567890\",\n  \"name\": \"John Doe\",\n  \"iat\": 1516239022\n}";
    private string _encoderSecret = "your-256-bit-secret";
    private string _selectedAlgorithm = "HS256";
    private string _encodedToken = string.Empty;
    private string _encoderStatusMessage = string.Empty;
    private bool _isEncoderError;

    /// <summary>Creates the JWT decoder view model with decoder and clipboard services.</summary>
    public JwtDecoderViewModel(IJwtDecoderService service, IClipboardService clipboard, IJwtEncoderService? encoder = null)
    {
        _service = service;
        _clipboard = clipboard;
        _encoder = encoder ?? new JwtEncoderService();
        DecodeCommand = new RelayCommand(_ => Decode());
        CopyHeaderCommand = new RelayCommand(_ => Copy(HeaderJson, "Header"));
        CopyPayloadCommand = new RelayCommand(_ => Copy(PayloadJson, "Payload"));
        ClearCommand = new RelayCommand(_ => Clear());
        ShowDecoderCommand = new RelayCommand(_ => IsEncoderMode = false);
        ShowEncoderCommand = new RelayCommand(_ => IsEncoderMode = true);
        CopyEncodedTokenCommand = new RelayCommand(_ => CopyEncodedToken());
        DecodeEncodedTokenCommand = new RelayCommand(_ => DecodeEncodedToken());
        Encode();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the supported encoder signing algorithms.</summary>
    public IReadOnlyList<string> Algorithms => _encoder.SupportedAlgorithms;

    /// <summary>Gets or sets whether the encoder tab is shown instead of the decoder tab.</summary>
    public bool IsEncoderMode
    {
        get => _isEncoderMode;
        set
        {
            if (_isEncoderMode == value)
            {
                return;
            }

            _isEncoderMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDecoderMode));
        }
    }

    /// <summary>Gets whether the decoder tab is shown.</summary>
    public bool IsDecoderMode => !_isEncoderMode;

    /// <summary>Gets or sets the header JSON used by the encoder.</summary>
    public string EncoderHeaderJson
    {
        get => _encoderHeaderJson;
        set
        {
            if (_encoderHeaderJson == value)
            {
                return;
            }

            _encoderHeaderJson = value;
            OnPropertyChanged();
            Encode();
        }
    }

    /// <summary>Gets or sets the payload JSON used by the encoder.</summary>
    public string EncoderPayloadJson
    {
        get => _encoderPayloadJson;
        set
        {
            if (_encoderPayloadJson == value)
            {
                return;
            }

            _encoderPayloadJson = value;
            OnPropertyChanged();
            Encode();
        }
    }

    /// <summary>Gets or sets the shared secret used to sign the encoded token.</summary>
    public string EncoderSecret
    {
        get => _encoderSecret;
        set
        {
            if (_encoderSecret == value)
            {
                return;
            }

            _encoderSecret = value;
            OnPropertyChanged();
            Encode();
        }
    }

    /// <summary>Gets or sets the selected signing algorithm and mirrors it into the header.</summary>
    public string SelectedAlgorithm
    {
        get => _selectedAlgorithm;
        set
        {
            if (_selectedAlgorithm == value || string.IsNullOrEmpty(value))
            {
                return;
            }

            _selectedAlgorithm = value;
            OnPropertyChanged();
            SyncHeaderAlgorithm();
            Encode();
        }
    }

    private void SyncHeaderAlgorithm()
    {
        try
        {
            if (System.Text.Json.Nodes.JsonNode.Parse(_encoderHeaderJson) is System.Text.Json.Nodes.JsonObject header)
            {
                header["alg"] = _selectedAlgorithm;
                _encoderHeaderJson = header.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                OnPropertyChanged(nameof(EncoderHeaderJson));
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Invalid header JSON is reported by the encoder status instead.
        }
    }

    /// <summary>Gets the most recently encoded compact token.</summary>
    public string EncodedToken => _encodedToken;

    /// <summary>Gets the encoder status message.</summary>
    public string EncoderStatusMessage => _encoderStatusMessage;

    /// <summary>Gets whether the latest encode attempt failed.</summary>
    public bool IsEncoderError => _isEncoderError;

    /// <summary>Gets the command that shows the decoder tab.</summary>
    public ICommand ShowDecoderCommand { get; }

    /// <summary>Gets the command that shows the encoder tab.</summary>
    public ICommand ShowEncoderCommand { get; }

    /// <summary>Gets the command that copies the encoded token.</summary>
    public ICommand CopyEncodedTokenCommand { get; }

    /// <summary>Gets the command that sends the encoded token to the decoder tab.</summary>
    public ICommand DecodeEncodedTokenCommand { get; }

    /// <summary>Gets or sets the compact JWT input and decodes it as it changes.</summary>
    public string TokenText
    {
        get => _tokenText;
        set
        {
            if (_tokenText == value)
            {
                return;
            }

            _tokenText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InputCharacterCount));
            if (string.IsNullOrWhiteSpace(value))
            {
                ResetDecoded();
            }
            else
            {
                Decode();
            }
        }
    }

    /// <summary>Gets the most recent parsing result.</summary>
    public JwtParseResult? Result => _result;

    /// <summary>Gets the formatted decoded header JSON.</summary>
    public string HeaderJson => _result?.HeaderJson ?? string.Empty;

    /// <summary>Gets the formatted decoded payload JSON.</summary>
    public string PayloadJson => _result?.PayloadJson ?? string.Empty;

    /// <summary>Gets decoded claims or an empty list when no valid token has been parsed.</summary>
    public IReadOnlyList<JwtClaimInfo> Claims => _result?.Claims ?? Array.Empty<JwtClaimInfo>();

    /// <summary>Gets the signature-segment status without asserting signature validation.</summary>
    public string SignatureSegmentStatus => _result is null
        ? string.Empty
        : _result.SignatureLength == 0
            ? "Signature segment is empty; not verified."
            : $"Signature segment present ({_result.SignatureLength:N0} Base64URL characters); not verified.";

    /// <summary>Gets whether valid decoded content is available.</summary>
    public bool HasDecodedContent => _result?.IsValid == true;

    /// <summary>Gets whether the latest decode attempt failed.</summary>
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

    /// <summary>Gets the status message shown below the input.</summary>
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

    /// <summary>Gets the input character count.</summary>
    public int InputCharacterCount => TokenText.Length;

    /// <summary>Gets the persistent notice that decoding does not verify signature or trust.</summary>
    public string UntrustedDataWarning =>
        "Untrusted data: decoding only displays token contents. It does NOT verify the signature, issuer, audience, expiration policy, or whether the token is safe to trust.";

    /// <summary>Gets the command that parses and displays the token.</summary>
    public ICommand DecodeCommand { get; }

    /// <summary>Gets the command that copies the decoded header JSON.</summary>
    public ICommand CopyHeaderCommand { get; }

    /// <summary>Gets the command that copies the decoded payload JSON.</summary>
    public ICommand CopyPayloadCommand { get; }

    /// <summary>Gets the command that clears the input and decoded data.</summary>
    public ICommand ClearCommand { get; }

    private void Decode()
    {
        _result = _service.Decode(TokenText);
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(HeaderJson));
        OnPropertyChanged(nameof(PayloadJson));
        OnPropertyChanged(nameof(Claims));
        OnPropertyChanged(nameof(SignatureSegmentStatus));
        OnPropertyChanged(nameof(HasDecodedContent));
        IsError = !_result.IsValid;
        StatusMessage = _result.Message;
    }

    private void Copy(string value, string label)
    {
        if (value.Length == 0)
        {
            IsError = true;
            StatusMessage = $"There is no decoded {label.ToLowerInvariant()} JSON to copy.";
            return;
        }

        var copied = _clipboard.TrySetText(value);
        IsError = !copied;
        StatusMessage = copied
            ? $"{label} JSON copied to the clipboard."
            : "Clipboard is unavailable or busy. Try copying again.";
    }

    private void Clear()
    {
        TokenText = string.Empty;
        ResetDecoded();
    }

    private void ResetDecoded()
    {
        _result = null;
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(HeaderJson));
        OnPropertyChanged(nameof(PayloadJson));
        OnPropertyChanged(nameof(Claims));
        OnPropertyChanged(nameof(SignatureSegmentStatus));
        OnPropertyChanged(nameof(HasDecodedContent));
        IsError = false;
        StatusMessage = "Paste a compact JWT to decode its header and payload locally.";
    }

    private void Encode()
    {
        var result = _encoder.Encode(_encoderHeaderJson, _encoderPayloadJson, _encoderSecret, _selectedAlgorithm);
        _encodedToken = result.Token;
        _encoderStatusMessage = result.Message;
        _isEncoderError = !result.IsValid;
        OnPropertyChanged(nameof(EncodedToken));
        OnPropertyChanged(nameof(EncoderStatusMessage));
        OnPropertyChanged(nameof(IsEncoderError));
    }

    private void CopyEncodedToken()
    {
        if (_encodedToken.Length == 0)
        {
            _isEncoderError = true;
            _encoderStatusMessage = "Fix the header and payload JSON before copying a token.";
        }
        else
        {
            var copied = _clipboard.TrySetText(_encodedToken);
            _isEncoderError = !copied;
            _encoderStatusMessage = copied
                ? "Encoded JWT copied to the clipboard."
                : "Clipboard is unavailable or busy. Try copying again.";
        }

        OnPropertyChanged(nameof(EncoderStatusMessage));
        OnPropertyChanged(nameof(IsEncoderError));
    }

    private void DecodeEncodedToken()
    {
        if (_encodedToken.Length == 0)
        {
            return;
        }

        TokenText = _encodedToken;
        IsEncoderMode = false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
