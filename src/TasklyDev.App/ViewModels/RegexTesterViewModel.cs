using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using TasklyDev.Core.RegularExpressions;

namespace TasklyDev.ViewModels;

public sealed class RegexTesterViewModel : INotifyPropertyChanged
{
    private readonly IRegexTesterService _service;
    private string _pattern = @"(\w+)@([\w.]+)";
    private string _inputText = "Contact support@example.com or sales@example.org.";
    private string _statusMessage = "Test the pattern against the sample text.";
    private bool _ignoreCase;
    private bool _multiline;
    private bool _singleline;
    private bool _isBusy;
    private bool _isError;
    private bool _hasTested;
    private bool _isTruncated;

    public RegexTesterViewModel(IRegexTesterService service)
    {
        _service = service;
        TestCommand = new AsyncRelayCommand(TestAsync);
        ClearCommand = new RelayCommand(_ => Clear());
        LoadSampleCommand = new RelayCommand(_ => LoadSample());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand TestCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand LoadSampleCommand { get; }
    public ObservableCollection<RegexMatchResult> Matches { get; } = [];

    public string Pattern
    {
        get => _pattern;
        set => SetProperty(ref _pattern, value);
    }

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IgnoreCase
    {
        get => _ignoreCase;
        set => SetProperty(ref _ignoreCase, value);
    }

    public bool Multiline
    {
        get => _multiline;
        set => SetProperty(ref _multiline, value);
    }

    public bool Singleline
    {
        get => _singleline;
        set => SetProperty(ref _singleline, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsError
    {
        get => _isError;
        private set
        {
            if (SetProperty(ref _isError, value))
            {
                OnPropertyChanged(nameof(HasNoMatches));
            }
        }
    }

    public bool HasTested
    {
        get => _hasTested;
        private set
        {
            if (SetProperty(ref _hasTested, value))
            {
                OnPropertyChanged(nameof(HasNoMatches));
            }
        }
    }

    public bool HasMatches => Matches.Count > 0;
    public bool HasNoMatches => HasTested && !IsError && !HasMatches;
    public bool IsNotBusy => !IsBusy;

    public bool IsTruncated
    {
        get => _isTruncated;
        private set => SetProperty(ref _isTruncated, value);
    }

    public async Task TestAsync()
    {
        if (string.IsNullOrWhiteSpace(Pattern))
        {
            ResetResults();
            HasTested = true;
            IsError = true;
            StatusMessage = "Enter a regular expression pattern.";
            return;
        }

        var pattern = Pattern;
        var input = InputText;
        var ignoreCase = IgnoreCase;
        var multiline = Multiline;
        var singleline = Singleline;
        IsBusy = true;
        IsError = false;
        HasTested = false;
        ResetResults();
        StatusMessage = "Testing pattern…";

        try
        {
            var result = await Task.Run(() => _service.Test(pattern, input, ignoreCase, multiline, singleline));
            foreach (var match in result.Matches)
            {
                Matches.Add(match);
            }

            IsTruncated = result.IsTruncated;
            HasTested = true;
            OnPropertyChanged(nameof(HasMatches));
            StatusMessage = result.IsTruncated
                ? $"Found at least {Matches.Count} matches. Results are capped at {RegexTesterService.MaximumDisplayedMatches}."
                : Matches.Count == 0
                    ? "No matches found."
                    : $"Found {Matches.Count} {(Matches.Count == 1 ? "match" : "matches")}.";
        }
        catch (TimeoutException)
        {
            HasTested = true;
            IsError = true;
            StatusMessage = "Regex evaluation timed out. Simplify the pattern or reduce the input size.";
        }
        catch (ArgumentException exception)
        {
            HasTested = true;
            IsError = true;
            StatusMessage = $"Invalid regular expression: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Clear()
    {
        Pattern = string.Empty;
        InputText = string.Empty;
        ResetResults();
        HasTested = false;
        IsError = false;
        StatusMessage = "Enter a pattern and sample text, then test.";
    }

    private void LoadSample()
    {
        Pattern = @"(\w+)@([\w.]+)";
        InputText = "Contact support@example.com or sales@example.org.";
        IgnoreCase = false;
        Multiline = false;
        Singleline = false;
        ResetResults();
        HasTested = false;
        IsError = false;
        StatusMessage = "Sample pattern and text loaded.";
    }

    private void ResetResults()
    {
        Matches.Clear();
        OnPropertyChanged(nameof(HasMatches));
        OnPropertyChanged(nameof(HasNoMatches));
        IsTruncated = false;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsBusy))
        {
            OnPropertyChanged(nameof(IsNotBusy));
        }

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
