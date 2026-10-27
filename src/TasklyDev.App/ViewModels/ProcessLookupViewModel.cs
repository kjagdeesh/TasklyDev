using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TasklyDev.Windows.Processes;

namespace TasklyDev.ViewModels;

public sealed class ProcessLookupViewModel : INotifyPropertyChanged
{
    private readonly IProcessSnapshotService _service;
    private readonly List<ProcessSnapshotEntry> _snapshot = [];
    private string _searchText = string.Empty;
    private string _statusMessage = "Refresh to capture a read-only snapshot of running processes.";
    private bool _isBusy;
    private bool _hasSnapshot;
    private bool _isError;

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
            var snapshot = await Task.Run(_service.GetSnapshot);
            _snapshot.Clear();
            _snapshot.AddRange(snapshot);
            HasSnapshot = true;
            ApplyFilter();
            StatusMessage = SearchText.Length == 0
                ? $"Showing {Processes.Count} of {_snapshot.Count} running processes."
                : $"Showing {Processes.Count} of {_snapshot.Count} running processes matching the search.";
        }
        catch (UnauthorizedAccessException)
        {
            Processes.Clear();
            _snapshot.Clear();
            HasSnapshot = false;
            IsError = true;
            StatusMessage = "Access was denied while reading the running process list. No elevation was requested.";
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(SnapshotCount));
        }
        catch (Win32Exception)
        {
            Processes.Clear();
            _snapshot.Clear();
            HasSnapshot = false;
            IsError = true;
            StatusMessage = "Windows could not provide the running process snapshot. Try refreshing again.";
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(SnapshotCount));
        }
        catch (InvalidOperationException)
        {
            Processes.Clear();
            _snapshot.Clear();
            HasSnapshot = false;
            IsError = true;
            StatusMessage = "The running process snapshot could not be read. Try refreshing again.";
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(SnapshotCount));
        }
        finally
        {
            IsBusy = false;
        }
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
