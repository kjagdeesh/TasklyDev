using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TasklyDev.Core.DotNet;

namespace TasklyDev.ViewModels;

/// <summary>Coordinates machine-wide .NET installation discovery and status presentation.</summary>
public sealed class DotNetInspectorViewModel : INotifyPropertyChanged
{
    private readonly IDotNetInspectionService _service;
    private string _statusMessage = "Refresh to inspect .NET installations available on this machine.";
    private string _dotNetPath = "Not inspected";
    private string _inspectionScope = "Machine-wide installation discovery; no project or global.json selection.";
    private string _workloadOutput = string.Empty;
    private string _infoOutput = string.Empty;
    private DateTimeOffset? _inspectedAt;
    private bool _isBusy;
    private bool _hasInspected;
    private bool _isError;

    /// <summary>Creates the .NET inspector with a read-only discovery service.</summary>
    public DotNetInspectorViewModel(IDotNetInspectionService service)
    {
        _service = service;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the SDK versions and installation locations discovered on the machine.</summary>
    public ObservableCollection<DotNetSdkInfo> Sdks { get; } = [];

    /// <summary>Gets the runtime versions and installation locations discovered on the machine.</summary>
    public ObservableCollection<DotNetRuntimeInfo> Runtimes { get; } = [];

    /// <summary>Gets diagnostics for each dotnet CLI command.</summary>
    public ObservableCollection<DotNetCommandDiagnostic> Diagnostics { get; } = [];

    /// <summary>Gets the command used to refresh machine-wide discovery information.</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>Gets the resolved dotnet host path or its unavailable state.</summary>
    public string DotNetPath
    {
        get => _dotNetPath;
        private set => SetProperty(ref _dotNetPath, value);
    }

    /// <summary>Gets the explicit machine-level discovery scope and working directory.</summary>
    public string InspectionScope
    {
        get => _inspectionScope;
        private set => SetProperty(ref _inspectionScope, value);
    }

    /// <summary>Gets raw read-only workload command output.</summary>
    public string WorkloadOutput
    {
        get => _workloadOutput;
        private set => SetProperty(ref _workloadOutput, value);
    }

    /// <summary>Gets raw `dotnet --info` output.</summary>
    public string InfoOutput
    {
        get => _infoOutput;
        private set => SetProperty(ref _infoOutput, value);
    }

    /// <summary>Gets the local time at which the latest inspection completed.</summary>
    public DateTimeOffset? InspectedAt
    {
        get => _inspectedAt;
        private set => SetProperty(ref _inspectedAt, value);
    }

    /// <summary>Gets the current user-facing status.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Gets whether discovery is in progress.</summary>
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

    /// <summary>Gets whether discovery has completed at least once.</summary>
    public bool HasInspected
    {
        get => _hasInspected;
        private set => SetProperty(ref _hasInspected, value);
    }

    /// <summary>Gets whether the latest discovery could not locate or use the .NET host.</summary>
    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    /// <summary>Gets whether the refresh command is idle.</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>Gets whether no SDKs were reported after a completed inspection.</summary>
    public bool HasNoSdks => HasInspected && Sdks.Count == 0;

    /// <summary>Gets whether no runtimes were reported after a completed inspection.</summary>
    public bool HasNoRuntimes => HasInspected && Runtimes.Count == 0;

    /// <summary>Gets whether no workload details were returned after inspection.</summary>
    public bool HasNoWorkloadOutput => HasInspected && string.IsNullOrWhiteSpace(WorkloadOutput);

    /// <summary>Asynchronously refreshes the bounded machine-wide SDK and workload snapshot.</summary>
    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsError = false;
        StatusMessage = "Inspecting machine-wide .NET SDKs, runtimes, and workloads…";

        try
        {
            var result = await _service.InspectAsync();
            Sdks.Clear();
            foreach (var sdk in result.Sdks)
            {
                Sdks.Add(sdk);
            }

            Runtimes.Clear();
            foreach (var runtime in result.Runtimes)
            {
                Runtimes.Add(runtime);
            }

            Diagnostics.Clear();
            foreach (var diagnostic in result.Diagnostics)
            {
                Diagnostics.Add(diagnostic);
            }

            DotNetPath = result.DotNetPaths.Count == 0
                ? "Unavailable"
                : string.Join(Environment.NewLine, result.DotNetPaths);
            InspectionScope = result.InspectionScope;
            WorkloadOutput = result.WorkloadOutput;
            InfoOutput = result.InfoOutput;
            InspectedAt = result.InspectedAt;
            HasInspected = true;
            IsError = !result.IsAvailable;
            StatusMessage = result.StatusMessage;
            OnPropertyChanged(nameof(HasNoSdks));
            OnPropertyChanged(nameof(HasNoRuntimes));
            OnPropertyChanged(nameof(HasNoWorkloadOutput));
        }
        catch (OperationCanceledException)
        {
            IsError = true;
            StatusMessage = "The .NET inspection was canceled.";
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
