using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TasklyDev.Core.Hashing;
using TasklyDev.Models;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

public sealed class HashCalculatorViewModel : INotifyPropertyChanged
{
    private static readonly HashAlgorithmKind[] AllAlgorithms =
    [
        HashAlgorithmKind.MD5,
        HashAlgorithmKind.SHA256,
        HashAlgorithmKind.SHA512
    ];

    private readonly IHashService _service;
    private readonly IClipboardService _clipboard;
    private readonly IFilePickerService _filePicker;
    private string _inputText = string.Empty;
    private string _filePath = string.Empty;
    private string _statusMessage = "Choose text or a file, select an algorithm, then calculate.";
    private HashInputMode _inputMode;
    private HashAlgorithmChoice _selectedAlgorithm = HashAlgorithmChoice.All;
    private bool _isBusy;
    private bool _isError;
    private CancellationTokenSource? _cancellationTokenSource;

    public HashCalculatorViewModel(
        IHashService service,
        IClipboardService clipboard,
        IFilePickerService filePicker)
    {
        _service = service;
        _clipboard = clipboard;
        _filePicker = filePicker;
        CalculateCommand = new AsyncRelayCommand(CalculateAsync);
        BrowseFileCommand = new RelayCommand(_ => BrowseFile());
        CopyDigestCommand = new RelayCommand(CopyDigest);
        ClearCommand = new RelayCommand(_ => Clear());
        CancelCommand = new RelayCommand(_ => _cancellationTokenSource?.Cancel(), _ => IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<HashDigest> Results { get; } = [];
    public Array Algorithms => Enum.GetValues<HashAlgorithmChoice>();
    public Array InputModes => Enum.GetValues<HashInputMode>();
    public ICommand CalculateCommand { get; }
    public ICommand BrowseFileCommand { get; }
    public ICommand CopyDigestCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand CancelCommand { get; }
    public bool HasResults => Results.Count > 0;
    public bool IsEmpty => !HasResults;
    public string ResultHeading => Results.Count == 1 ? "Hash result" : $"Hash results · {Results.Count} algorithms";

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
            OnPropertyChanged(nameof(InputCharacterCount));
            OnPropertyChanged(nameof(InputByteCount));
        }
    }

    public int InputCharacterCount => InputText.Length;
    public int InputByteCount => System.Text.Encoding.UTF8.GetByteCount(InputText);

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value)
            {
                return;
            }

            _filePath = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public HashInputMode InputMode
    {
        get => _inputMode;
        set
        {
            if (_inputMode == value)
            {
                return;
            }

            _inputMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTextMode));
            OnPropertyChanged(nameof(IsFileMode));
        }
    }

    public bool IsTextMode
    {
        get => InputMode == HashInputMode.Text;
        set
        {
            if (value)
            {
                InputMode = HashInputMode.Text;
            }
        }
    }

    public bool IsFileMode
    {
        get => InputMode == HashInputMode.File;
        set
        {
            if (value)
            {
                InputMode = HashInputMode.File;
            }
        }
    }

    public HashAlgorithmChoice SelectedAlgorithm
    {
        get => _selectedAlgorithm;
        set
        {
            if (_selectedAlgorithm == value)
            {
                return;
            }

            _selectedAlgorithm = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotBusy));
            ((RelayCommand)CancelCommand).NotifyCanExecuteChanged();
        }
    }

    public bool IsNotBusy => !IsBusy;

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

    public async Task CalculateAsync()
    {
        Results.Clear();
        NotifyResultsChanged();

        if (InputMode == HashInputMode.File && string.IsNullOrWhiteSpace(FilePath))
        {
            SetStatus("Choose a file before calculating its hash.", isError: true);
            return;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        _cancellationTokenSource = cancellationTokenSource;
        IsBusy = true;
        SetStatus(InputMode == HashInputMode.File ? "Hashing file locally…" : "Calculating text digest…", isError: false);

        try
        {
            var algorithms = GetSelectedAlgorithms();
            var inputText = InputText;
            var filePath = FilePath;
            IReadOnlyList<HashDigest> digests;
            if (InputMode == HashInputMode.File)
            {
                digests = await _service.ComputeFileAsync(filePath, algorithms, cancellationTokenSource.Token);
            }
            else
            {
                digests = await Task.Run(
                    () => _service.ComputeText(inputText, algorithms),
                    cancellationTokenSource.Token);
            }

            foreach (var digest in digests)
            {
                Results.Add(digest);
            }

            NotifyResultsChanged();
            SetStatus("Hash calculation completed. Source content is not logged or saved.", isError: false);
        }
        catch (OperationCanceledException)
        {
            Results.Clear();
            NotifyResultsChanged();
            SetStatus("Hash calculation cancelled.", isError: false);
        }
        catch (FileNotFoundException)
        {
            SetStatus("The selected file could not be found. Select it again and retry.", isError: true);
        }
        catch (DirectoryNotFoundException)
        {
            SetStatus("The selected file location could not be found. Select it again and retry.", isError: true);
        }
        catch (UnauthorizedAccessException)
        {
            SetStatus("Access to the selected file was denied.", isError: true);
        }
        catch (IOException)
        {
            SetStatus("The selected file could not be read. Check that it is available and retry.", isError: true);
        }
        catch (CryptographicException)
        {
            SetStatus("The selected hash algorithm could not be initialized.", isError: true);
        }
        finally
        {
            IsBusy = false;
            cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void BrowseFile()
    {
        var selectedFile = _filePicker.PickFile();
        if (selectedFile is not null)
        {
            FilePath = selectedFile;
            InputMode = HashInputMode.File;
            SetStatus("File selected. Choose an algorithm and calculate its digest.", isError: false);
        }
    }

    private void CopyDigest(object? parameter)
    {
        if (parameter is not HashDigest digest)
        {
            SetStatus("There is no digest to copy.", isError: true);
            return;
        }

        if (_clipboard.TrySetText(digest.HexValue))
        {
            SetStatus($"{digest.Algorithm} digest copied to the clipboard.", isError: false);
        }
        else
        {
            SetStatus("Clipboard is unavailable or busy. Try copying again.", isError: true);
        }
    }

    private void Clear()
    {
        _cancellationTokenSource?.Cancel();
        InputText = string.Empty;
        FilePath = string.Empty;
        Results.Clear();
        NotifyResultsChanged();
        SetStatus("Hash input and results cleared.", isError: false);
    }

    private IReadOnlyCollection<HashAlgorithmKind> GetSelectedAlgorithms() =>
        SelectedAlgorithm switch
        {
            HashAlgorithmChoice.All => AllAlgorithms,
            HashAlgorithmChoice.MD5 => [HashAlgorithmKind.MD5],
            HashAlgorithmChoice.SHA256 => [HashAlgorithmKind.SHA256],
            HashAlgorithmChoice.SHA512 => [HashAlgorithmKind.SHA512],
            _ => throw new ArgumentOutOfRangeException(nameof(SelectedAlgorithm), SelectedAlgorithm, "Unsupported hash selection.")
        };

    private void NotifyResultsChanged()
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ResultHeading));
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
    }

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
