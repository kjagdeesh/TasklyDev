using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TasklyDev.Core.SystemInformation;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

/// <summary>Coordinates system-information refresh, copy, and report-export actions.</summary>
public sealed class SystemInformationViewModel : INotifyPropertyChanged
{
    private static readonly string[] SummaryFieldNames =
    [
        "Windows edition",
        "Machine name",
        "OS architecture",
        "Processor",
        "Installed physical memory",
        "System uptime"
    ];

    private readonly ISystemInformationService _service;
    private readonly IClipboardService _clipboard;
    private readonly ISystemInformationReportExportService _exportService;
    private SystemInformationSnapshot? _snapshot;
    private DateTimeOffset? _generatedAt;
    private string _statusMessage = "Refresh to collect read-only information about this machine.";
    private bool _isBusy;
    private bool _hasLoaded;
    private bool _isError;
    private bool _hasUnavailableInformation;

    /// <summary>Creates the System Information screen model and its user actions.</summary>
    public SystemInformationViewModel(
        ISystemInformationService service,
        IClipboardService clipboard,
        ISystemInformationReportExportService exportService)
    {
        _service = service;
        _clipboard = clipboard;
        _exportService = exportService;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CopySummaryCommand = new RelayCommand(_ => CopySummary());
        ExportTextCommand = new AsyncRelayCommand(ExportTextAsync);
        ExportJsonCommand = new AsyncRelayCommand(ExportJsonAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the collected information grouped by category.</summary>
    public ObservableCollection<SystemInformationGroup> Groups { get; } = [];

    /// <summary>Gets key values from the current snapshot for the overview cards.</summary>
    public ObservableCollection<SystemInformationField> SummaryFields { get; } = [];

    /// <summary>Gets the action that refreshes machine information.</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>Gets the action that copies the current report as plain text.</summary>
    public ICommand CopySummaryCommand { get; }

    /// <summary>Gets the action that exports the current snapshot as TXT.</summary>
    public ICommand ExportTextCommand { get; }

    /// <summary>Gets the action that exports the current snapshot as JSON.</summary>
    public ICommand ExportJsonCommand { get; }

    /// <summary>Gets the time at which the displayed snapshot was generated.</summary>
    public DateTimeOffset? GeneratedAt
    {
        get => _generatedAt;
        private set => SetProperty(ref _generatedAt, value);
    }

    /// <summary>Gets the current user-facing status.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Gets whether a refresh or export action is in progress.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    /// <summary>Gets whether a snapshot has been loaded.</summary>
    public bool HasLoaded
    {
        get => _hasLoaded;
        private set => SetProperty(ref _hasLoaded, value);
    }

    /// <summary>Gets whether the latest refresh produced no available fields.</summary>
    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    /// <summary>Gets whether some information fields could not be collected.</summary>
    public bool HasUnavailableInformation
    {
        get => _hasUnavailableInformation;
        private set => SetProperty(ref _hasUnavailableInformation, value);
    }

    /// <summary>Gets whether actions are available.</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>Gets whether the page has loaded but contains no available values.</summary>
    public bool HasNoAvailableInformation => HasLoaded && IsError;

    /// <summary>Asynchronously refreshes the current machine-information snapshot.</summary>
    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsError = false;
        StatusMessage = "Collecting system information…";
        try
        {
            var snapshot = await _service.GetSnapshotAsync();
            _snapshot = snapshot;
            Groups.Clear();
            foreach (var group in snapshot.Groups)
            {
                Groups.Add(group);
            }

            SummaryFields.Clear();
            var snapshotFields = snapshot.Groups.SelectMany(group => group.Fields).ToArray();
            foreach (var fieldName in SummaryFieldNames)
            {
                var field = snapshotFields.FirstOrDefault(value => value.Name == fieldName);
                if (field is not null)
                {
                    SummaryFields.Add(field);
                }
            }

            GeneratedAt = snapshot.GeneratedAt;
            HasLoaded = true;
            IsError = !snapshot.HasAvailableInformation;
            HasUnavailableInformation = snapshot.HasUnavailableInformation;
            StatusMessage = IsError
                ? "No system information could be collected. Review the field-level availability details."
                : HasUnavailableInformation
                    ? "System information refreshed; some fields are unavailable or incomplete."
                    : "System information refreshed.";
            OnPropertyChanged(nameof(HasNoAvailableInformation));
        }
        catch (OperationCanceledException)
        {
            IsError = true;
            StatusMessage = "System information collection was canceled.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CopySummary()
    {
        if (_snapshot is null)
        {
            StatusMessage = "Refresh system information before copying a summary.";
            IsError = true;
            return;
        }

        var summary = SystemInformationReportFormatter.Format(_snapshot, SystemInformationExportFormat.Txt);
        if (_clipboard.TrySetText(summary))
        {
            StatusMessage = "System information summary copied to the clipboard.";
            IsError = false;
        }
        else
        {
            StatusMessage = "The summary could not be copied. Try again after checking clipboard availability.";
            IsError = true;
        }
    }

    /// <summary>Exports the current snapshot as a text report.</summary>
    public Task ExportTextAsync() => ExportAsync(SystemInformationExportFormat.Txt);

    /// <summary>Exports the current snapshot as a JSON report.</summary>
    public Task ExportJsonAsync() => ExportAsync(SystemInformationExportFormat.Json);

    private async Task ExportAsync(SystemInformationExportFormat format)
    {
        if (IsBusy)
        {
            return;
        }

        if (_snapshot is null)
        {
            StatusMessage = "Refresh system information before exporting a report.";
            IsError = true;
            return;
        }

        IsBusy = true;
        IsError = false;
        try
        {
            var report = SystemInformationReportFormatter.Format(_snapshot, format);
            var result = await _exportService.ExportAsync(report, format);
            if (result.WasCancelled)
            {
                StatusMessage = "Report export was canceled.";
            }
            else if (result.Succeeded)
            {
                StatusMessage = $"Report saved to {result.FilePath}.";
            }
            else
            {
                StatusMessage = $"Report export failed: {result.ErrorMessage ?? "No details were returned."}";
                IsError = true;
            }
        }
        finally
        {
            IsBusy = false;
        }
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
