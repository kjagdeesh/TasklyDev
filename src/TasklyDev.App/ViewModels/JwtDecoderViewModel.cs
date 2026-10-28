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
    private readonly IClipboardService _clipboard;
    private string _tokenText = string.Empty;
    private JwtParseResult? _result;
    private string _statusMessage = "Paste a compact JWT to decode its header and payload locally.";
    private bool _isError;

    /// <summary>Creates the JWT decoder view model with decoder and clipboard services.</summary>
    public JwtDecoderViewModel(IJwtDecoderService service, IClipboardService clipboard)
    {
        _service = service;
        _clipboard = clipboard;
        DecodeCommand = new RelayCommand(_ => Decode());
        CopyHeaderCommand = new RelayCommand(_ => Copy(HeaderJson, "Header"));
        CopyPayloadCommand = new RelayCommand(_ => Copy(PayloadJson, "Payload"));
        ClearCommand = new RelayCommand(_ => Clear());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or sets the compact JWT input.</summary>
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
