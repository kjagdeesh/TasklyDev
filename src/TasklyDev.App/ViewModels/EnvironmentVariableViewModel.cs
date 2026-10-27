using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security;
using System.Windows.Input;
using TasklyDev.Services;
using TasklyDev.Windows.Environment;

namespace TasklyDev.ViewModels;

public sealed class EnvironmentVariableViewModel : INotifyPropertyChanged
{
    private readonly IEnvironmentVariableService _service;
    private readonly IClipboardService _clipboard;
    private readonly List<EnvironmentVariableEntry> _snapshot = [];
    private EnvironmentVariableScope _selectedScope = EnvironmentVariableScope.Process;
    private EnvironmentVariableEntry? _selectedVariable;
    private string _searchText = string.Empty;
    private string _statusMessage = "Choose a scope to view its environment variables.";
    private bool _isBusy;
    private bool _hasLoaded;
    private bool _isError;

    public EnvironmentVariableViewModel(IEnvironmentVariableService service, IClipboardService clipboard)
    {
        _service = service;
        _clipboard = clipboard;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CopyValueCommand = new RelayCommand(_ => CopySelectedValue());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RefreshCommand { get; }
    public ICommand CopyValueCommand { get; }
    public ObservableCollection<EnvironmentVariableEntry> Variables { get; } = [];
    public Array Scopes => Enum.GetValues<EnvironmentVariableScope>();

    public EnvironmentVariableScope SelectedScope
    {
        get => _selectedScope;
        set
        {
            if (!SetProperty(ref _selectedScope, value))
            {
                return;
            }

            _ = RefreshAsync();
        }
    }

    public EnvironmentVariableEntry? SelectedVariable
    {
        get => _selectedVariable;
        set
        {
            if (!SetProperty(ref _selectedVariable, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedValue));
        }
    }

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

    public bool HasLoaded
    {
        get => _hasLoaded;
        private set
        {
            if (SetProperty(ref _hasLoaded, value))
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

    public bool HasSelection => SelectedVariable is not null;
    public string SelectedValue => SelectedVariable?.Value ?? string.Empty;
    public bool HasResults => Variables.Count > 0;
    public bool HasNoResults => HasLoaded && !IsError && !HasResults;
    public int SnapshotCount => _snapshot.Count;

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsError = false;
        StatusMessage = $"Reading {SelectedScope.ToString().ToLowerInvariant()} environment variables…";
        try
        {
            var scope = SelectedScope;
            var variables = await Task.Run(() => _service.GetVariables(scope));
            if (scope != SelectedScope)
            {
                return;
            }

            _snapshot.Clear();
            _snapshot.AddRange(variables);
            HasLoaded = true;
            ApplyFilter();
            StatusMessage = SearchText.Length == 0
                ? $"Showing {Variables.Count} of {_snapshot.Count} {scope.ToString().ToLowerInvariant()} variables."
                : $"Showing {Variables.Count} of {_snapshot.Count} {scope.ToString().ToLowerInvariant()} variables matching the search.";
        }
        catch (UnauthorizedAccessException)
        {
            FailRefresh("Access was denied while reading this scope. No elevation was requested.");
        }
        catch (SecurityException)
        {
            FailRefresh("Windows security policy prevented access to this scope.");
        }
        catch (NotSupportedException)
        {
            FailRefresh("This environment variable scope is not supported on this system.");
        }
        catch (ArgumentException)
        {
            FailRefresh("The selected environment variable scope is not supported.");
        }
        catch (InvalidOperationException)
        {
            FailRefresh("The environment variables could not be read. Try refreshing again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void FailRefresh(string message)
    {
        _snapshot.Clear();
        Variables.Clear();
        SelectedVariable = null;
        HasLoaded = false;
        IsError = true;
        StatusMessage = message;
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(SnapshotCount));
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = term.Length == 0
            ? _snapshot
            : _snapshot.Where(variable =>
                variable.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                variable.Value.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

        Variables.Clear();
        foreach (var variable in filtered)
        {
            Variables.Add(variable);
        }

        if (SelectedVariable is not null && !Variables.Contains(SelectedVariable))
        {
            SelectedVariable = null;
        }

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasNoResults));
        OnPropertyChanged(nameof(SnapshotCount));
    }

    private void CopySelectedValue()
    {
        if (SelectedVariable is null)
        {
            return;
        }

        var succeeded = _clipboard.TrySetText(SelectedVariable.Value);
        IsError = !succeeded;
        StatusMessage = succeeded
            ? "Full variable value copied to the clipboard."
            : "Could not copy the full variable value to the clipboard.";
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
