using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Input;
using TasklyDev.Core.ClipboardHistory;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

public enum ClipboardHistorySortMode
{
    NewestFirst,
    OldestFirst,
    ClipboardTextAscending,
    ClipboardTextDescending,
    PinnedOnly,
    LargestFirst,
    SmallestFirst
}

public sealed record ClipboardHistorySortOption(string Label, ClipboardHistorySortMode Mode);

/// <summary>Coordinates explicit clipboard capture, encrypted history, search, and entry actions.</summary>
public sealed class ClipboardHistoryViewModel : INotifyPropertyChanged
{
    private const int MaximumPendingClipboardItems = 32;
    private const double MaximumStorageMegabytes = 100;
    private static readonly IReadOnlyList<ClipboardHistorySortOption> AvailableSortOptions =
    [
        new("Newest first", ClipboardHistorySortMode.NewestFirst),
        new("Oldest first", ClipboardHistorySortMode.OldestFirst),
        new("Clipboard text A-Z", ClipboardHistorySortMode.ClipboardTextAscending),
        new("Clipboard text Z-A", ClipboardHistorySortMode.ClipboardTextDescending),
        new("Pinned only", ClipboardHistorySortMode.PinnedOnly),
        new("Largest first", ClipboardHistorySortMode.LargestFirst),
        new("Smallest first", ClipboardHistorySortMode.SmallestFirst)
    ];

    private readonly IClipboardHistoryService _service;
    private readonly IClipboardService _clipboard;
    private readonly IClipboardHistoryConfirmationService _confirmation;
    private readonly Queue<string> _pendingClipboardItems = new();
    private ClipboardHistoryEntry? _selectedEntry;
    private ClipboardHistoryEntry? _selectedListEntry;
    private string? _copiedTextToSuppress;
    private DateTimeOffset _copySuppressionExpiresAt;
    private string _actionFeedback = string.Empty;
    private bool _isActionError;
    private string _searchText = string.Empty;
    private string _statusMessage = "Clipboard capture is off. Start capture only after reviewing the disclosure.";
    private string _storagePath = string.Empty;
    private string _storageUsage = "History storage not loaded.";
    private double _storageUsageMegabytes;
    private DateTimeOffset? _lastUpdatedAt;
    private ClipboardHistorySortOption _selectedSortOption = AvailableSortOptions[0];
    private ClipboardCaptureState _captureState;
    private bool _isBusy;
    private bool _hasLoaded;
    private bool _isError;
    private bool _consentConfirmed;
    private bool _isDrainingClipboardQueue;

    /// <summary>Creates the clipboard-history model and its user actions.</summary>
    public ClipboardHistoryViewModel(
        IClipboardHistoryService service,
        IClipboardService clipboard,
        IClipboardHistoryConfirmationService confirmation)
    {
        _service = service;
        _clipboard = clipboard;
        _confirmation = confirmation;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        StartCaptureCommand = new AsyncRelayCommand(StartCaptureAsync);
        PauseCaptureCommand = new AsyncRelayCommand(PauseCaptureAsync);
        ResumeCaptureCommand = new AsyncRelayCommand(ResumeCaptureAsync);
        DisableCaptureCommand = new AsyncRelayCommand(DisableCaptureAsync);
        ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);
        CopySelectedCommand = new RelayCommand(_ => CopySelected());
        TogglePinSelectedCommand = new AsyncRelayCommand(TogglePinSelectedAsync);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync);
        ClearAllCommand = new AsyncRelayCommand(ClearAllAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<bool>? CaptureStateChanged;

    /// <summary>Gets the complete local history in newest-first order.</summary>
    public ObservableCollection<ClipboardHistoryEntry> Entries { get; } = [];

    /// <summary>Gets the history entries that match the search query.</summary>
    public ObservableCollection<ClipboardHistoryEntry> FilteredEntries { get; } = [];

    public IReadOnlyList<ClipboardHistorySortOption> SortOptions => AvailableSortOptions;

    public ICommand RefreshCommand { get; }
    public ICommand StartCaptureCommand { get; }
    public ICommand PauseCaptureCommand { get; }
    public ICommand ResumeCaptureCommand { get; }
    public ICommand DisableCaptureCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ClipboardHistorySortOption SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                ApplyFilter();
            }
        }
    }

    public ICommand CopySelectedCommand { get; }
    public ICommand TogglePinSelectedCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand ClearAllCommand { get; }

    /// <summary>Gets or sets the history text filter.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(HasSearchText));
                ApplyFilter();
            }
        }
    }

    /// <summary>Gets whether a search query is available to clear.</summary>
    public bool HasSearchText => SearchText.Length > 0;

    /// <summary>Gets or sets explicit confirmation of the capture disclosure.</summary>
    public bool ConsentConfirmed
    {
        get => _consentConfirmed;
        set
        {
            if (SetProperty(ref _consentConfirmed, value))
            {
                OnPropertyChanged(nameof(CanStartCapture));
            }
        }
    }

    /// <summary>Gets the persisted capture state.</summary>
    public ClipboardCaptureState CaptureState
    {
        get => _captureState;
        private set
        {
            if (SetProperty(ref _captureState, value))
            {
                OnPropertyChanged(nameof(IsCapturing));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(IsCaptureDisabled));
                OnPropertyChanged(nameof(CanStartCapture));
                OnPropertyChanged(nameof(CanUseSelectedEntry));
                if (!IsCapturing)
                {
                    _pendingClipboardItems.Clear();
                }

                CaptureStateChanged?.Invoke(this, IsCapturing);
            }
        }
    }

    /// <summary>Gets whether clipboard capture is actively enabled.</summary>
    public bool IsCapturing => CaptureState == ClipboardCaptureState.Capturing;

    /// <summary>Gets whether capture is paused.</summary>
    public bool IsPaused => CaptureState == ClipboardCaptureState.Paused;

    /// <summary>Gets whether capture is disabled.</summary>
    public bool IsCaptureDisabled => CaptureState == ClipboardCaptureState.Disabled;

    /// <summary>Gets the current storage status or action result.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Gets the encrypted JSON storage file location.</summary>
    public string StoragePath
    {
        get => _storagePath;
        private set => SetProperty(ref _storagePath, value);
    }

    /// <summary>Gets current storage size and entry count.</summary>
    public string StorageUsage
    {
        get => _storageUsage;
        private set => SetProperty(ref _storageUsage, value);
    }

    /// <summary>Gets the stored history size in megabytes, capped at the storage limit.</summary>
    public double StorageUsageMegabytes
    {
        get => _storageUsageMegabytes;
        private set => SetProperty(ref _storageUsageMegabytes, value);
    }

    /// <summary>Gets the filtered and total history entry counts.</summary>
    public string HistoryCountSummary => $"{FilteredEntries.Count:N0} of {Entries.Count:N0} entries";

    /// <summary>Gets the time at which history was most recently updated.</summary>
    public DateTimeOffset? LastUpdatedAt
    {
        get => _lastUpdatedAt;
        private set => SetProperty(ref _lastUpdatedAt, value);
    }

    /// <summary>Gets whether a history operation is in progress.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(CanStartCapture));
                OnPropertyChanged(nameof(CanUseSelectedEntry));
            }
        }
    }

    /// <summary>Gets whether history has been loaded at least once.</summary>
    public bool HasLoaded
    {
        get => _hasLoaded;
        private set => SetProperty(ref _hasLoaded, value);
    }

    /// <summary>Gets whether the latest storage operation failed.</summary>
    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    /// <summary>Gets whether buttons can be used.</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>Gets whether explicit consent and the idle state allow capture to start.</summary>
    public bool CanStartCapture => ConsentConfirmed && IsNotBusy && IsCaptureDisabled;

    /// <summary>Gets whether selected-entry actions are currently available.</summary>
    public bool CanUseSelectedEntry => HasSelectedEntry && IsNotBusy;

    /// <summary>Gets whether no history entries are present after a load.</summary>
    public bool HasNoEntries => HasLoaded && Entries.Count == 0;

    /// <summary>Gets whether a non-empty search query matches no entries.</summary>
    public bool HasNoSearchResults => HasLoaded && Entries.Count > 0 && FilteredEntries.Count == 0 && SearchText.Length > 0;

    /// <summary>Gets whether the current filter has entries to display.</summary>
    public bool HasFilteredEntries => FilteredEntries.Count > 0;

    /// <summary>Loads saved history and resumes capture only for a prior explicit opt-in.</summary>
    public Task InitializeAsync() => RefreshAsync();

    /// <summary>Reloads the persisted history and capture state.</summary>
    public async Task RefreshAsync()
    {
        await RunStoreOperationAsync(
            () => _service.LoadAsync(),
            "History refreshed.",
            updateCaptureState: true).ConfigureAwait(true);
    }

    /// <summary>Captures a text value supplied by the active Windows clipboard listener.</summary>
    public async Task CaptureTextAsync(string text)
    {
        if (!IsCapturing)
        {
            return;
        }

        if (_copiedTextToSuppress is { } copiedText)
        {
            var shouldSuppress = DateTimeOffset.UtcNow <= _copySuppressionExpiresAt &&
                                 string.Equals(text, copiedText, StringComparison.Ordinal);
            _copiedTextToSuppress = null;
            if (shouldSuppress)
            {
                return;
            }
        }

        if (_pendingClipboardItems.Count >= MaximumPendingClipboardItems)
        {
            ReportClipboardReadFailure("The history writer is busy; this clipboard update was skipped.");
            return;
        }

        _pendingClipboardItems.Enqueue(text);
        await DrainPendingClipboardItemsAsync().ConfigureAwait(true);
    }

    /// <summary>Reports a clipboard-read failure without exposing clipboard contents.</summary>
    public void ReportClipboardReadFailure(string message)
    {
        StatusMessage = $"Clipboard text could not be read: {message}";
        IsError = true;
    }

    /// <summary>Reports that clipboard text exceeded the per-entry size limit.</summary>
    public void ReportOversizedClipboardText() =>
        ReportClipboardTextSkipped("Clipboard text exceeded the 1 MB entry limit and was skipped.");

    /// <summary>Reports a clipboard value that the configured policy intentionally skipped.</summary>
    public void ReportClipboardTextSkipped(string message)
    {
        StatusMessage = message;
        IsError = false;
    }

    /// <summary>Starts capture only after the user checks the explicit consent control.</summary>
    public async Task StartCaptureAsync()
    {
        if (!ConsentConfirmed)
        {
            StatusMessage = "Confirm the capture disclosure before starting clipboard history.";
            IsError = true;
            return;
        }

        await RunStoreOperationAsync(
            () => _service.SetCaptureStateAsync(ClipboardCaptureState.Capturing),
            "Clipboard capture started.",
            updateCaptureState: true).ConfigureAwait(true);
        if (IsCapturing)
        {
            ConsentConfirmed = false;
        }
    }

    /// <summary>Pauses capture while retaining the user's prior opt-in and saved history.</summary>
    public Task PauseCaptureAsync() =>
        RunStoreOperationAsync(
            () => _service.SetCaptureStateAsync(ClipboardCaptureState.Paused),
            "Clipboard capture paused.",
            updateCaptureState: true);

    /// <summary>Resumes capture under the prior explicit opt-in.</summary>
    public Task ResumeCaptureAsync() =>
        RunStoreOperationAsync(
            () => _service.SetCaptureStateAsync(ClipboardCaptureState.Capturing),
            "Clipboard capture resumed.",
            updateCaptureState: true);

    /// <summary>Disables capture and clears the remembered opt-in without deleting history.</summary>
    public async Task DisableCaptureAsync()
    {
        await RunStoreOperationAsync(
            () => _service.SetCaptureStateAsync(ClipboardCaptureState.Disabled),
            "Clipboard capture disabled; saved history was retained.",
            updateCaptureState: true).ConfigureAwait(true);
        ConsentConfirmed = false;
    }

    /// <summary>Copies the selected entry without logging or persisting additional data.</summary>
    public void CopySelected()
    {
        if (SelectedEntry is null)
        {
            StatusMessage = "Select a history entry before copying.";
            IsError = true;
            return;
        }

        var text = SelectedEntry.Text;
        if (_clipboard.TrySetText(text))
        {
            _copiedTextToSuppress = text;
            _copySuppressionExpiresAt = DateTimeOffset.UtcNow.AddSeconds(3);
            StatusMessage = "Selected text copied to the clipboard.";
            IsError = false;
            SetActionFeedback(StatusMessage, isError: false);
        }
        else
        {
            StatusMessage = "Clipboard access is unavailable; the selected text was not copied.";
            IsError = true;
            SetActionFeedback(StatusMessage, isError: true);
        }
    }

    /// <summary>Updates the selected entry's pin state.</summary>
    public async Task TogglePinSelectedAsync()
    {
        if (SelectedEntry is null)
        {
            StatusMessage = "Select a history entry before changing its pin state.";
            IsError = true;
            SetActionFeedback(StatusMessage, isError: true);
            return;
        }

        var id = SelectedEntry.Id;
        var isPinned = !SelectedEntry.IsPinned;
        await RunStoreOperationAsync(
            () => _service.SetPinnedAsync(id, isPinned),
            isPinned ? "History entry pinned." : "History entry unpinned.",
            updateCaptureState: false).ConfigureAwait(true);
        SetActionFeedback(StatusMessage, IsError);
    }

    /// <summary>Deletes the selected entry from local encrypted storage.</summary>
    public Task DeleteSelectedAsync()
    {
        if (SelectedEntry is null)
        {
            StatusMessage = "Select a history entry before deleting it.";
            IsError = true;
            return Task.CompletedTask;
        }

        if (!_confirmation.ConfirmDeleteSelectedEntry())
        {
            StatusMessage = "Delete entry was canceled.";
            IsError = false;
            return Task.CompletedTask;
        }

        var id = SelectedEntry.Id;
        return RunStoreOperationAsync(
            () => _service.DeleteAsync(id),
            "History entry deleted.",
            updateCaptureState: false);
    }

    /// <summary>Confirms and deletes every history entry while preserving capture state.</summary>
    public Task ClearAllAsync()
    {
        if (!_confirmation.ConfirmClearHistory())
        {
            StatusMessage = "Clear history was canceled.";
            IsError = false;
            return Task.CompletedTask;
        }

        return RunStoreOperationAsync(
            () => _service.ClearAsync(),
            "All history entries were deleted.",
            updateCaptureState: false);
    }

    /// <summary>Gets or sets the selected history entry.</summary>
    public ClipboardHistoryEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                OnPropertyChanged(nameof(HasSelectedEntry));
                OnPropertyChanged(nameof(HasNoSelectedEntry));
                OnPropertyChanged(nameof(SelectedEntryIsPinned));
                OnPropertyChanged(nameof(SelectedEntryPinAction));
                OnPropertyChanged(nameof(CanUseSelectedEntry));
            }
        }
    }

    /// <summary>Gets or sets the entry currently selected in the visible list.</summary>
    public ClipboardHistoryEntry? SelectedListEntry
    {
        get => _selectedListEntry;
        set
        {
            if (SetProperty(ref _selectedListEntry, value) && value is not null)
            {
                SelectedEntry = value;
            }
        }
    }

    /// <summary>Gets the most recent copy or pin action feedback.</summary>
    public string ActionFeedback
    {
        get => _actionFeedback;
        private set
        {
            if (SetProperty(ref _actionFeedback, value))
            {
                OnPropertyChanged(nameof(HasActionFeedback));
            }
        }
    }

    /// <summary>Gets whether the most recent copy or pin action failed.</summary>
    public bool IsActionError
    {
        get => _isActionError;
        private set => SetProperty(ref _isActionError, value);
    }

    /// <summary>Gets whether copy or pin feedback is available.</summary>
    public bool HasActionFeedback => ActionFeedback.Length > 0;

    /// <summary>Gets whether an entry is selected.</summary>
    public bool HasSelectedEntry => SelectedEntry is not null;

    /// <summary>Gets whether no entry is selected for preview.</summary>
    public bool HasNoSelectedEntry => !HasSelectedEntry;

    /// <summary>Gets whether the selected entry is pinned.</summary>
    public bool SelectedEntryIsPinned => SelectedEntry?.IsPinned == true;

    /// <summary>Gets the selected-entry pin button label.</summary>
    public string SelectedEntryPinAction => SelectedEntryIsPinned ? "Unpin" : "Pin";

    private async Task RunStoreOperationAsync(
        Func<Task<ClipboardHistorySnapshot>> operation,
        string fallbackStatus,
        bool updateCaptureState)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsError = false;
        try
        {
            var snapshot = await operation().ConfigureAwait(true);
            ApplySnapshot(snapshot, updateCaptureState);
            if (snapshot.StatusMessage.Length == 0)
            {
                StatusMessage = fallbackStatus;
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                CryptographicException or JsonException or System.Security.SecurityException or
                System.ComponentModel.Win32Exception or FormatException)
        {
            StatusMessage = $"Clipboard history storage failed: {exception.Message}";
            IsError = true;
        }
        finally
        {
            IsBusy = false;
            if (!_isDrainingClipboardQueue && _pendingClipboardItems.Count > 0 && IsCapturing)
            {
                await DrainPendingClipboardItemsAsync().ConfigureAwait(true);
            }
        }
    }

    private async Task DrainPendingClipboardItemsAsync()
    {
        if (_isDrainingClipboardQueue || IsBusy || !IsCapturing)
        {
            return;
        }

        _isDrainingClipboardQueue = true;
        try
        {
            while (_pendingClipboardItems.Count > 0 && IsCapturing)
            {
                var text = _pendingClipboardItems.Dequeue();
                await RunStoreOperationAsync(
                    () => _service.CaptureAsync(text),
                    "Clipboard text processed.",
                    updateCaptureState: false).ConfigureAwait(true);
            }
        }
        finally
        {
            _isDrainingClipboardQueue = false;
        }
    }

    private void ApplySnapshot(ClipboardHistorySnapshot snapshot, bool updateCaptureState)
    {
        var selectedId = SelectedEntry?.Id;
        Entries.Clear();
        foreach (var entry in snapshot.Entries)
        {
            Entries.Add(entry);
        }

        StoragePath = snapshot.StoragePath;
        StorageUsageMegabytes = Math.Clamp(snapshot.StorageSizeBytes / 1024d / 1024d, 0, MaximumStorageMegabytes);
        StorageUsage = $"{StorageUsageMegabytes:N2} MB of {MaximumStorageMegabytes:N0} MB";
        LastUpdatedAt = DateTimeOffset.Now;
        StatusMessage = snapshot.StatusMessage;
        IsError = snapshot.StatusMessage.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                  snapshot.StatusMessage.Contains("skipped", StringComparison.OrdinalIgnoreCase) ||
                  snapshot.StatusMessage.Contains("ignored", StringComparison.OrdinalIgnoreCase);
        HasLoaded = true;
        if (updateCaptureState)
        {
            CaptureState = snapshot.CaptureState;
        }

        SelectedEntry = selectedId is null
            ? null
            : Entries.FirstOrDefault(entry => entry.Id == selectedId);
        ApplyFilter();
        OnPropertyChanged(nameof(HasNoEntries));
        OnPropertyChanged(nameof(HasNoSearchResults));
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var selectedId = SelectedEntry?.Id;
        var pinnedOnly = SelectedSortOption.Mode == ClipboardHistorySortMode.PinnedOnly;
        var matches = Entries.Where(entry =>
            (!pinnedOnly || entry.IsPinned) &&
            (term.Length == 0 || entry.Text.Contains(term, StringComparison.OrdinalIgnoreCase)));
        var sorted = SelectedSortOption.Mode switch
        {
            ClipboardHistorySortMode.OldestFirst => matches.OrderBy(entry => entry.CapturedAt),
            ClipboardHistorySortMode.ClipboardTextAscending =>
                matches.OrderBy(entry => entry.Text.TrimStart(), StringComparer.OrdinalIgnoreCase),
            ClipboardHistorySortMode.ClipboardTextDescending =>
                matches.OrderByDescending(entry => entry.Text.TrimStart(), StringComparer.OrdinalIgnoreCase),
            ClipboardHistorySortMode.LargestFirst => matches.OrderByDescending(entry => entry.Utf8ByteCount),
            ClipboardHistorySortMode.SmallestFirst => matches.OrderBy(entry => entry.Utf8ByteCount),
            ClipboardHistorySortMode.PinnedOnly => matches.OrderByDescending(entry => entry.CapturedAt),
            _ => matches.OrderByDescending(entry => entry.CapturedAt)
        };
        var filtered = pinnedOnly ? sorted : sorted.OrderByDescending(entry => entry.IsPinned);
        var visibleEntries = filtered.ToList();

        FilteredEntries.Clear();
        foreach (var entry in visibleEntries)
        {
            FilteredEntries.Add(entry);
        }

        SelectedListEntry = selectedId is null
            ? null
            : visibleEntries.FirstOrDefault(entry => entry.Id == selectedId);
        if (selectedId is not null && SelectedEntry?.Id == selectedId)
        {
            SelectedEntry = Entries.FirstOrDefault(entry => entry.Id == selectedId) ?? SelectedEntry;
        }

        OnPropertyChanged(nameof(HasNoEntries));
        OnPropertyChanged(nameof(HasNoSearchResults));
        OnPropertyChanged(nameof(HasFilteredEntries));
        OnPropertyChanged(nameof(HistoryCountSummary));
    }

    private void SetActionFeedback(string message, bool isError)
    {
        ActionFeedback = message;
        IsActionError = isError;
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
