using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TasklyDev.Core.Time;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

/// <summary>Coordinates timestamp/date conversion, result presentation, and copy actions.</summary>
public sealed class UnixTimestampViewModel : INotifyPropertyChanged
{
    private readonly ITimestampService _service;
    private readonly IClipboardService _clipboard;
    private string _timestampInput = string.Empty;
    private string _dateTimeInput = string.Empty;
    private string _utcResult = string.Empty;
    private string _localResult = string.Empty;
    private string _secondsResult = string.Empty;
    private string _millisecondsResult = string.Empty;
    private string _statusMessage = "Choose seconds or milliseconds, then enter a timestamp.";
    private UnixTimestampUnit _selectedUnit = UnixTimestampUnit.Seconds;
    private bool _isTimestampMode = true;
    private bool _isError;

    /// <summary>Creates the timestamp converter with conversion and clipboard services.</summary>
    public UnixTimestampViewModel(ITimestampService service, IClipboardService clipboard)
    {
        _service = service;
        _clipboard = clipboard;
        ConvertTimestampCommand = new RelayCommand(_ => ConvertTimestamp());
        ConvertDateCommand = new RelayCommand(_ => ConvertDate());
        NowCommand = new RelayCommand(_ => SetNow());
        LoadSampleCommand = new RelayCommand(_ => LoadSample());
        ClearCommand = new RelayCommand(_ => Clear());
        CopyUtcCommand = new RelayCommand(_ => Copy(UtcResult));
        CopyLocalCommand = new RelayCommand(_ => Copy(LocalResult));
        CopySecondsCommand = new RelayCommand(_ => Copy(SecondsResult));
        CopyMillisecondsCommand = new RelayCommand(_ => Copy(MillisecondsResult));
        ModeCommand = new RelayCommand(parameter =>
        {
            if (parameter is bool isTimestampMode)
            {
                IsTimestampMode = isTimestampMode;
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ConvertTimestampCommand { get; }
    public ICommand ConvertDateCommand { get; }
    public ICommand NowCommand { get; }
    public ICommand LoadSampleCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand CopyUtcCommand { get; }
    public ICommand CopyLocalCommand { get; }
    public ICommand CopySecondsCommand { get; }
    public ICommand CopyMillisecondsCommand { get; }
    public ICommand ModeCommand { get; }

    public Array Units => Enum.GetValues<UnixTimestampUnit>();
    public string LocalTimeZoneLabel => $"Local · {TimeZoneInfo.Local.DisplayName}";

    public UnixTimestampUnit SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            if (_selectedUnit == value)
            {
                return;
            }

            _selectedUnit = value;
            OnPropertyChanged();
        }
    }

    public bool IsTimestampMode
    {
        get => _isTimestampMode;
        set
        {
            if (_isTimestampMode == value)
            {
                return;
            }

            _isTimestampMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDateMode));
        }
    }

    public bool IsDateMode
    {
        get => !IsTimestampMode;
        set => IsTimestampMode = !value;
    }

    public string TimestampInput
    {
        get => _timestampInput;
        set
        {
            if (_timestampInput == value)
            {
                return;
            }

            _timestampInput = value;
            OnPropertyChanged();
        }
    }

    public string DateTimeInput
    {
        get => _dateTimeInput;
        set
        {
            if (_dateTimeInput == value)
            {
                return;
            }

            _dateTimeInput = value;
            OnPropertyChanged();
        }
    }

    public string UtcResult
    {
        get => _utcResult;
        private set => SetProperty(ref _utcResult, value);
    }

    public string LocalResult
    {
        get => _localResult;
        private set => SetProperty(ref _localResult, value);
    }

    public string SecondsResult
    {
        get => _secondsResult;
        private set => SetProperty(ref _secondsResult, value);
    }

    public string MillisecondsResult
    {
        get => _millisecondsResult;
        private set => SetProperty(ref _millisecondsResult, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
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

    private void ConvertTimestamp()
    {
        if (!long.TryParse(TimestampInput.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var timestamp))
        {
            ClearResults();
            SetStatus("Enter a whole-number Unix timestamp. The selected unit is not inferred.", isError: true);
            return;
        }

        try
        {
            var value = _service.FromUnixTimestamp(timestamp, SelectedUnit);
            SetDateResults(value);
            SetStatus($"Converted the explicitly selected {UnitLabel(SelectedUnit)} timestamp.", isError: false);
        }
        catch (ArgumentOutOfRangeException)
        {
            ClearResults();
            SetStatus("Timestamp is outside the supported date range.", isError: true);
        }
    }

    private void ConvertDate()
    {
        try
        {
            var value = _service.ParseIso8601WithOffset(DateTimeInput);
            UtcResult = value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            LocalResult = value.ToLocalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
            SecondsResult = _service.ToUnixTimestamp(value, UnixTimestampUnit.Seconds).ToString(CultureInfo.InvariantCulture);
            MillisecondsResult = _service.ToUnixTimestamp(value, UnixTimestampUnit.Milliseconds).ToString(CultureInfo.InvariantCulture);
            SetStatus("Converted date/time using its explicit UTC marker or numeric offset.", isError: false);
        }
        catch (FormatException exception)
        {
            ClearResults();
            SetStatus(exception.Message, isError: true);
        }
        catch (ArgumentOutOfRangeException)
        {
            ClearResults();
            SetStatus("Date/time is outside the supported Unix timestamp range.", isError: true);
        }
    }

    private void SetDateResults(DateTimeOffset value)
    {
        UtcResult = value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        LocalResult = value.ToLocalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
        SecondsResult = _service.ToUnixTimestamp(value, UnixTimestampUnit.Seconds).ToString(CultureInfo.InvariantCulture);
        MillisecondsResult = _service.ToUnixTimestamp(value, UnixTimestampUnit.Milliseconds).ToString(CultureInfo.InvariantCulture);
    }

    private void SetNow()
    {
        var now = DateTimeOffset.UtcNow;
        if (IsTimestampMode)
        {
            TimestampInput = _service.ToUnixTimestamp(now, SelectedUnit).ToString(CultureInfo.InvariantCulture);
            ConvertTimestamp();
        }
        else
        {
            DateTimeInput = now.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            ConvertDate();
        }
    }

    private void LoadSample()
    {
        SelectedUnit = UnixTimestampUnit.Seconds;
        if (IsTimestampMode)
        {
            TimestampInput = "1718105680";
            ConvertTimestamp();
        }
        else
        {
            DateTimeInput = "2024-06-11T11:34:40+00:00";
            ConvertDate();
        }
    }

    private void Clear()
    {
        TimestampInput = string.Empty;
        DateTimeInput = string.Empty;
        ClearResults();
        SetStatus("Timestamp and date/time values cleared.", isError: false);
    }

    private void Copy(string value)
    {
        if (value.Length == 0)
        {
            SetStatus("There is no result to copy.", isError: true);
            return;
        }

        if (_clipboard.TrySetText(value))
        {
            SetStatus("Timestamp result copied to the clipboard.", isError: false);
        }
        else
        {
            SetStatus("Clipboard is unavailable or busy. Try copying again.", isError: true);
        }
    }

    private void ClearResults()
    {
        UtcResult = string.Empty;
        LocalResult = string.Empty;
        SecondsResult = string.Empty;
        MillisecondsResult = string.Empty;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
    }

    private static string UnitLabel(UnixTimestampUnit unit) =>
        unit == UnixTimestampUnit.Seconds ? "seconds" : "milliseconds";

    private void SetProperty(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
