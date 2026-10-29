using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Formats.Asn1;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Windows.Input;
using TasklyDev.Core.Certificates;
using TasklyDev.Services;

namespace TasklyDev.ViewModels;

/// <summary>Coordinates local certificate-file and Windows-store inspection.</summary>
public sealed class CertificateInspectorViewModel : INotifyPropertyChanged
{
    private readonly ICertificateInspectorService _service;
    private readonly ICertificateFilePickerService _filePicker;
    private CertificateStoreScope _selectedStoreScope = CertificateStoreScope.CurrentUser;
    private CertificateListItem? _selectedStoreCertificate;
    private CertificateInspectionResult? _inspection;
    private string _statusMessage = "Choose a public certificate file or load a Windows personal store.";
    private string _selectedFilePath = string.Empty;
    private bool _isBusy;
    private bool _isError;

    /// <summary>Creates the certificate inspector and its user actions.</summary>
    public CertificateInspectorViewModel(
        ICertificateInspectorService service,
        ICertificateFilePickerService filePicker)
    {
        _service = service;
        _filePicker = filePicker;
        StoreCertificates.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasStoreCertificates));
        BrowseCertificateCommand = new AsyncRelayCommand(BrowseCertificateAsync);
        LoadStoreCommand = new AsyncRelayCommand(LoadStoreAsync);
        InspectSelectedStoreCertificateCommand = new AsyncRelayCommand(InspectSelectedStoreCertificateAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the metadata entries from the selected personal store.</summary>
    public ObservableCollection<CertificateListItem> StoreCertificates { get; } = [];

    /// <summary>Gets whether the selected store currently contains certificates to display.</summary>
    public bool HasStoreCertificates => StoreCertificates.Count > 0;

    /// <summary>Gets available Windows personal-store scopes.</summary>
    public IReadOnlyList<CertificateStoreScope> StoreScopes { get; } =
        Enum.GetValues<CertificateStoreScope>();

    public ICommand BrowseCertificateCommand { get; }
    public ICommand LoadStoreCommand { get; }
    public ICommand InspectSelectedStoreCertificateCommand { get; }

    /// <summary>Gets or sets the Windows personal-store location to browse.</summary>
    public CertificateStoreScope SelectedStoreScope
    {
        get => _selectedStoreScope;
        set
        {
            if (SetProperty(ref _selectedStoreScope, value))
            {
                StoreCertificates.Clear();
                SelectedStoreCertificate = null;
                Inspection = null;
                SelectedFilePath = string.Empty;
                StatusMessage = $"Load the {value} personal store to view its certificates.";
                IsError = false;
            }
        }
    }

    /// <summary>Gets or sets the selected store certificate.</summary>
    public CertificateListItem? SelectedStoreCertificate
    {
        get => _selectedStoreCertificate;
        set
        {
            if (SetProperty(ref _selectedStoreCertificate, value))
            {
                OnPropertyChanged(nameof(HasSelectedStoreCertificate));
                OnPropertyChanged(nameof(CanInspectSelectedStoreCertificate));
            }
        }
    }

    /// <summary>Gets the current public certificate details.</summary>
    public CertificateInspectionResult? Inspection
    {
        get => _inspection;
        private set
        {
            if (SetProperty(ref _inspection, value))
            {
                OnPropertyChanged(nameof(HasInspection));
            }
        }
    }

    /// <summary>Gets the selected public certificate file path.</summary>
    public string SelectedFilePath
    {
        get => _selectedFilePath;
        private set
        {
            if (SetProperty(ref _selectedFilePath, value))
            {
                OnPropertyChanged(nameof(HasSelectedFilePath));
            }
        }
    }

    /// <summary>Gets the current user-facing status.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Gets whether an inspection operation is active.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(CanInspectSelectedStoreCertificate));
            }
        }
    }

    /// <summary>Gets whether the latest operation failed.</summary>
    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    /// <summary>Gets whether an operation can start.</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>Gets whether a certificate has been inspected.</summary>
    public bool HasInspection => Inspection is not null;

    /// <summary>Gets whether a certificate file path is selected.</summary>
    public bool HasSelectedFilePath => !string.IsNullOrWhiteSpace(SelectedFilePath);

    /// <summary>Gets whether a store certificate is selected.</summary>
    public bool HasSelectedStoreCertificate => SelectedStoreCertificate is not null;

    /// <summary>Gets whether a selected store certificate can be inspected.</summary>
    public bool CanInspectSelectedStoreCertificate => HasSelectedStoreCertificate && IsNotBusy;

    /// <summary>Opens a public certificate file and inspects it locally.</summary>
    public async Task BrowseCertificateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var filePath = _filePicker.PickCertificateFile();
        if (filePath is null)
        {
            StatusMessage = "Certificate file selection was canceled.";
            IsError = false;
            return;
        }

        SelectedFilePath = filePath;
        SelectedStoreCertificate = null;
        Inspection = null;
        await RunInspectionAsync(
            () => _service.InspectFileAsync(filePath),
            $"Certificate inspected from {filePath}.");
    }

    /// <summary>Loads certificate metadata from the selected Windows personal store.</summary>
    public async Task LoadStoreAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsError = false;
        StatusMessage = $"Reading the {SelectedStoreScope} personal certificate store…";
        try
        {
            var certificates = await _service.GetStoreCertificatesAsync(SelectedStoreScope);
            StoreCertificates.Clear();
            foreach (var certificate in certificates)
            {
                StoreCertificates.Add(certificate);
            }

            SelectedStoreCertificate = null;
            Inspection = null;
            SelectedFilePath = string.Empty;
            StatusMessage = certificates.Count == 0
                ? $"The {SelectedStoreScope} personal certificate store contains no certificates."
                : $"Loaded {certificates.Count:N0} public certificate entries from the {SelectedStoreScope} personal store.";
        }
        catch (Exception exception) when (IsExpectedInspectionException(exception))
        {
            ReportFailure(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Inspects the selected certificate by thumbprint from its Windows store.</summary>
    public async Task InspectSelectedStoreCertificateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (SelectedStoreCertificate is null)
        {
            StatusMessage = "Load a store and select a certificate before inspecting it.";
            IsError = true;
            return;
        }

        var certificate = SelectedStoreCertificate;
        await RunInspectionAsync(
            () => _service.InspectStoreCertificateAsync(SelectedStoreScope, certificate.Thumbprint),
            $"Certificate inspected from the {SelectedStoreScope} personal store.");
        if (Inspection is not null)
        {
            SelectedFilePath = string.Empty;
        }
    }

    private async Task RunInspectionAsync(
        Func<Task<CertificateInspectionResult>> operation,
        string successMessage)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        IsError = false;
        StatusMessage = "Inspecting public certificate and building a local trust chain…";
        try
        {
            Inspection = await operation();
            StatusMessage = successMessage;
        }
        catch (Exception exception) when (IsExpectedInspectionException(exception))
        {
            Inspection = null;
            ReportFailure(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReportFailure(Exception exception)
    {
        StatusMessage = $"Certificate inspection failed: {exception.Message}";
        IsError = true;
    }

    private static bool IsExpectedInspectionException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or
            CryptographicException or ArgumentException or NotSupportedException or
            System.ComponentModel.Win32Exception or AsnContentException;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
