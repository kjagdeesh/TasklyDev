using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using TasklyDev.Core.Colors;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

/// <summary>Coordinates color parsing, converted output, preview state, and color selection.</summary>
public sealed class ColorConverterViewModel : INotifyPropertyChanged
{
    private readonly IColorService _service;
    private readonly IClipboardService _clipboard;
    private readonly IColorPickerService? _colorPicker;
    private string _inputText = "#F5B942";
    private string _hexValue = string.Empty;
    private string _rgbValue = string.Empty;
    private string _hslValue = string.Empty;
    private string _statusMessage = "Enter a color in HEX, RGB, or HSL format.";
    private ColorInputFormat _selectedFormat = ColorInputFormat.Hex;
    private Brush _previewBrush = new SolidColorBrush(Color.FromRgb(245, 185, 66));
    private bool _isError;
    private bool _hasColor;
    private bool _isPickingColor;

    /// <summary>Creates the converter with color, clipboard, and optional color-picker services.</summary>
    public ColorConverterViewModel(
        IColorService service,
        IClipboardService clipboard,
        IColorPickerService? colorPicker = null)
    {
        _service = service;
        _clipboard = clipboard;
        _colorPicker = colorPicker;
        CopyHexCommand = new RelayCommand(_ => Copy(HexValue));
        CopyRgbCommand = new RelayCommand(_ => Copy(RgbValue));
        CopyHslCommand = new RelayCommand(_ => Copy(HslValue));
        ClearCommand = new RelayCommand(_ => Clear());
        LoadSampleCommand = new RelayCommand(_ => LoadSample());
        PickColorCommand = new AsyncRelayCommand(PickColorAsync);
        UpdateColor();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand CopyHexCommand { get; }
    public ICommand CopyRgbCommand { get; }
    public ICommand CopyHslCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand LoadSampleCommand { get; }
    public ICommand PickColorCommand { get; }
    public Array InputFormats => Enum.GetValues<ColorInputFormat>();

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
            UpdateColor();
        }
    }

    public ColorInputFormat SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (_selectedFormat == value)
            {
                return;
            }

            _selectedFormat = value;
            OnPropertyChanged();
            UpdateColor();
        }
    }

    public string HexValue
    {
        get => _hexValue;
        private set => SetProperty(ref _hexValue, value);
    }

    public string RgbValue
    {
        get => _rgbValue;
        private set => SetProperty(ref _rgbValue, value);
    }

    public string HslValue
    {
        get => _hslValue;
        private set => SetProperty(ref _hslValue, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public Brush PreviewBrush
    {
        get => _previewBrush;
        private set => SetProperty(ref _previewBrush, value);
    }

    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    public bool HasColor
    {
        get => _hasColor;
        private set => SetProperty(ref _hasColor, value);
    }

    public bool IsPickingColor
    {
        get => _isPickingColor;
        private set
        {
            if (SetProperty(ref _isPickingColor, value))
            {
                OnPropertyChanged(nameof(IsNotPickingColor));
            }
        }
    }

    public bool IsNotPickingColor => !IsPickingColor;

    /// <summary>Opens the color dialog and updates the input when a color is selected.</summary>
    public async Task PickColorAsync()
    {
        if (_colorPicker is null)
        {
            IsError = true;
            StatusMessage = "Color selection is unavailable on this device.";
            return;
        }

        IsPickingColor = true;
        IsError = false;
        StatusMessage = "Choose a color in the Windows color dialog.";
        try
        {
            var initialColor = HasColor
                ? _service.ParseHex(HexValue)
                : new ColorValue(245, 185, 66);
            var color = await _colorPicker.PickColorAsync(initialColor);
            if (color is null)
            {
                IsError = false;
                StatusMessage = "Color selection canceled. Your previous color was kept.";
                return;
            }

            SelectedFormat = ColorInputFormat.Hex;
            InputText = _service.ToHex(color.Value);
            StatusMessage = $"Color selected: {HexValue}.";
        }
        catch (Win32Exception)
        {
            IsError = true;
            StatusMessage = "Windows could not open the color selection dialog. Try again.";
        }
        catch (InvalidOperationException exception)
        {
            IsError = true;
            StatusMessage = exception.Message;
        }
        finally
        {
            IsPickingColor = false;
        }
    }

    private void UpdateColor()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            ClearOutputs();
            IsError = false;
            StatusMessage = "Enter a color in HEX, RGB, or HSL format.";
            return;
        }

        try
        {
            var color = SelectedFormat switch
            {
                ColorInputFormat.Hex => _service.ParseHex(InputText),
                ColorInputFormat.Rgb => _service.ParseRgb(InputText),
                ColorInputFormat.Hsl => _service.ParseHsl(InputText),
                _ => throw new InvalidOperationException("Unsupported color input format.")
            };

            HexValue = _service.ToHex(color);
            RgbValue = _service.ToRgb(color);
            HslValue = _service.ToHsl(color);
            PreviewBrush = new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
            HasColor = true;
            IsError = false;
            StatusMessage = "Color converted. Alpha is preserved in each output format.";
        }
        catch (FormatException exception)
        {
            ClearOutputs();
            IsError = true;
            StatusMessage = exception.Message;
        }
    }

    private void Copy(string value)
    {
        if (!HasColor)
        {
            return;
        }

        var succeeded = _clipboard.TrySetText(value);
        IsError = !succeeded;
        StatusMessage = succeeded ? "Color value copied." : "Could not copy the color value to the clipboard.";
    }

    private void Clear()
    {
        InputText = string.Empty;
    }

    private void LoadSample()
    {
        SelectedFormat = ColorInputFormat.Hex;
        InputText = "#3A86FFB3";
    }

    private void ClearOutputs()
    {
        HexValue = string.Empty;
        RgbValue = string.Empty;
        HslValue = string.Empty;
        PreviewBrush = new SolidColorBrush(System.Windows.Media.Colors.Transparent);
        HasColor = false;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
