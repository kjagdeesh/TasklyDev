using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TasklyDev.Windows.Processes;

namespace TasklyDev.ViewModels;

/// <summary>Loads, filters, and sorts a read-only snapshot of running processes.</summary>
public sealed class ProcessLookupViewModel : INotifyPropertyChanged
{
    private readonly IProcessSnapshotService _service;
    private readonly Dispatcher? _dispatcher = Application.Current?.Dispatcher;
    private readonly List<ProcessSnapshotEntry> _snapshot = [];
    private string _searchText = string.Empty;
    private string _statusMessage = "Refresh to capture a read-only snapshot of running processes.";
    private bool _isBusy;
    private bool _hasSnapshot;
    private bool _isError;

    /// <summary>Creates the process lookup view model with its snapshot service.</summary>
    public ProcessLookupViewModel(IProcessSnapshotService service)
    {
        _service = service;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RefreshCommand { get; }
    public ObservableCollection<ProcessSnapshotEntry> Processes { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            ApplyFilter();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

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

    public bool IsNotBusy => !IsBusy;

    public bool HasSnapshot
    {
        get => _hasSnapshot;
        private set
        {
            if (SetProperty(ref _hasSnapshot, value))
            {
                OnPropertyChanged(nameof(HasNoResults));
            }
        }
    }

    public bool IsError
    {
        get => _isError;
        private set
        {
            if (SetProperty(ref _isError, value))
            {
                OnPropertyChanged(nameof(HasNoResults));
            }
        }
    }

    public bool HasResults => Processes.Count > 0;
    public bool HasNoResults => HasSnapshot && !IsError && !HasResults;
    public int SnapshotCount => _snapshot.Count;

    /// <summary>Asynchronously reloads the process snapshot and reapplies the active filter.</summary>
    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsError = false;
        StatusMessage = "Reading running process information…";

        try
        {
            var snapshot = await Task.Run(_service.GetSnapshot).ConfigureAwait(false);
            await InvokeOnDispatcherAsync(() =>
            {
                _snapshot.Clear();
                _snapshot.AddRange(snapshot);
                HasSnapshot = true;
                ApplyFilter();
                StatusMessage = SearchText.Length == 0
                    ? $"Showing {Processes.Count} of {_snapshot.Count} running processes."
                    : $"Showing {Processes.Count} of {_snapshot.Count} running processes matching the search.";
            });
        }
        catch (UnauthorizedAccessException)
        {
            await InvokeOnDispatcherAsync(() => FailRefresh(
                "Access was denied while reading the running process list. No elevation was requested."));
        }
        catch (Win32Exception)
        {
            await InvokeOnDispatcherAsync(() =>
                FailRefresh("Windows could not provide the running process snapshot. Try refreshing again."));
        }
        catch (InvalidOperationException)
        {
            await InvokeOnDispatcherAsync(() =>
                FailRefresh("The running process snapshot could not be read. Try refreshing again."));
        }
        finally
        {
            await InvokeOnDispatcherAsync(() => IsBusy = false);
        }
    }

    private void FailRefresh(string message)
    {
        Processes.Clear();
        _snapshot.Clear();
        HasSnapshot = false;
        IsError = true;
        StatusMessage = message;
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(SnapshotCount));
    }

    private Task InvokeOnDispatcherAsync(Action action)
    {
        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return _dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished
            ? Task.CompletedTask
            : _dispatcher.InvokeAsync(action, DispatcherPriority.DataBind).Task;
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        IEnumerable<ProcessSnapshotEntry> filtered;
        if (term.Length == 0)
        {
            filtered = _snapshot;
        }
        else if (term.All(char.IsDigit))
        {
            filtered = int.TryParse(term, out var processId)
                ? _snapshot.Where(process => process.Id == processId)
                : [];
        }
        else
        {
            filtered = _snapshot.Where(process => process.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        Processes.Clear();
        foreach (var process in filtered)
        {
            Processes.Add(process);
        }

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasNoResults));
        OnPropertyChanged(nameof(SnapshotCount));
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
