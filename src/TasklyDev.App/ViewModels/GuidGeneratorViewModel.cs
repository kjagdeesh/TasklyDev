using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TasklyDev.Core.Identifiers;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

/// <summary>Coordinates GUID generation options, generated rows, and clipboard actions.</summary>
public sealed class GuidGeneratorViewModel : INotifyPropertyChanged
{
    private readonly IGuidService _service;
    private readonly IClipboardService _clipboard;
    private string _quantityInput = "1";
    private GuidOutputFormat _selectedFormat = GuidOutputFormat.D;
    private bool _uppercase;
    private string _statusMessage = "Choose a format and quantity, then generate identifiers.";
    private bool _isError;

    /// <summary>Creates the GUID generator with generation and clipboard services.</summary>
    public GuidGeneratorViewModel(IGuidService service, IClipboardService clipboard)
    {
        _service = service;
        _clipboard = clipboard;
        GenerateCommand = new RelayCommand(_ => Generate());
        CopyAllCommand = new RelayCommand(_ => CopyAll());
        CopyItemCommand = new RelayCommand(CopyItem);
        ResetCommand = new RelayCommand(_ => Reset());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Values { get; } = [];
    public Array Formats => Enum.GetValues<GuidOutputFormat>();
    public bool IsEmpty => !HasValues;
    public ICommand GenerateCommand { get; }
    public ICommand CopyAllCommand { get; }
    public ICommand CopyItemCommand { get; }
    public ICommand ResetCommand { get; }

    public string QuantityInput
    {
        get => _quantityInput;
        set
        {
            if (_quantityInput == value)
            {
                return;
            }

            _quantityInput = value;
            OnPropertyChanged();
        }
    }

    public GuidOutputFormat SelectedFormat
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
            OnPropertyChanged(nameof(FormatDescription));
        }
    }

    public string FormatDescription => SelectedFormat switch
    {
        GuidOutputFormat.D => "Hyphenated: 00000000-0000-0000-0000-000000000000",
        GuidOutputFormat.N => "Digits only: 00000000000000000000000000000000",
        GuidOutputFormat.B => "{00000000-0000-0000-0000-000000000000}",
        GuidOutputFormat.P => "(00000000-0000-0000-0000-000000000000)",
        GuidOutputFormat.X => "{0x00000000,0x0000,0x0000,{0x00,...}}",
        _ => string.Empty
    };

    public bool Uppercase
    {
        get => _uppercase;
        set
        {
            if (_uppercase == value)
            {
                return;
            }

            _uppercase = value;
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

    public bool HasValues => Values.Count > 0;
    public string ResultHeading => Values.Count == 1 ? "Generated GUID" : $"Generated {Values.Count} GUIDs";

    private void Generate()
    {
        if (!int.TryParse(QuantityInput, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity))
        {
            Values.Clear();
            OnPropertyChanged(nameof(HasValues));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ResultHeading));
            SetStatus($"Enter a whole-number quantity between 1 and {GuidService.MaximumBatchSize}.", isError: true);
            return;
        }

        try
        {
            var generated = _service.Generate(quantity, SelectedFormat, Uppercase);
            Values.Clear();
            foreach (var value in generated)
            {
                Values.Add(value);
            }

            OnPropertyChanged(nameof(HasValues));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ResultHeading));
            SetStatus($"Generated {Values.Count} GUID{(Values.Count == 1 ? string.Empty : "s")}. GUIDs are identifiers, not secrets.", isError: false);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            Values.Clear();
            OnPropertyChanged(nameof(HasValues));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ResultHeading));
            SetStatus(exception.Message, isError: true);
        }
    }

    private void CopyItem(object? parameter)
    {
        if (parameter is not string value || value.Length == 0)
        {
            SetStatus("There is no GUID to copy.", isError: true);
            return;
        }

        SetCopyStatus(_clipboard.TrySetText(value));
    }

    private void CopyAll()
    {
        if (Values.Count == 0)
        {
            SetStatus("Generate at least one GUID before copying.", isError: true);
            return;
        }

        SetCopyStatus(_clipboard.TrySetText(string.Join(Environment.NewLine, Values)));
    }

    private void SetCopyStatus(bool copied)
    {
        SetStatus(
            copied ? "GUID copied to the clipboard." : "Clipboard is unavailable or busy. Try copying again.",
            isError: !copied);
    }

    private void Reset()
    {
        QuantityInput = "1";
        SelectedFormat = GuidOutputFormat.D;
        Uppercase = false;
        Values.Clear();
        OnPropertyChanged(nameof(HasValues));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ResultHeading));
        SetStatus("GUID generator settings reset.", isError: false);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
