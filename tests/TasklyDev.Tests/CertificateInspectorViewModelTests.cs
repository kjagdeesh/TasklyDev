using TasklyDev.Core.Certificates;
using TasklyDev.Services;
using TasklyDev.ViewModels;
using Xunit;

namespace TasklyDev.Tests;

public sealed class CertificateInspectorViewModelTests
{
    [Fact]
    public async Task LoadsAndInspectsSelectedStoreCertificate()
    {
        var service = new FakeCertificateInspectorService();
        var viewModel = new CertificateInspectorViewModel(service, new FixedCertificateFilePicker(null));

        await viewModel.LoadStoreAsync();
        viewModel.SelectedStoreCertificate = Assert.Single(viewModel.StoreCertificates);
        await viewModel.InspectSelectedStoreCertificateAsync();

        Assert.Equal(CertificateStoreScope.CurrentUser, service.LastStoreScope);
        Assert.Equal("thumbprint", service.LastInspectedThumbprint);
        Assert.Equal("Windows Current User personal store", Assert.IsType<CertificateInspectionResult>(viewModel.Inspection).Source);
        Assert.False(viewModel.IsError);
    }

    [Fact]
    public async Task ReportsStoreAccessErrorsExplicitly()
    {
        var service = new FakeCertificateInspectorService
        {
            StoreError = new UnauthorizedAccessException("access denied")
        };
        var viewModel = new CertificateInspectorViewModel(service, new FixedCertificateFilePicker(null));

        await viewModel.LoadStoreAsync();

        Assert.True(viewModel.IsError);
        Assert.Contains("access denied", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportsCanceledFileSelectionWithoutDiscardingTheCurrentInspection()
    {
        var service = new FakeCertificateInspectorService();
        var viewModel = new CertificateInspectorViewModel(service, new FixedCertificateFilePicker(null));

        await viewModel.LoadStoreAsync();
        viewModel.SelectedStoreCertificate = Assert.Single(viewModel.StoreCertificates);
        await viewModel.InspectSelectedStoreCertificateAsync();
        var inspection = viewModel.Inspection;

        await viewModel.BrowseCertificateAsync();

        Assert.Same(inspection, viewModel.Inspection);
        Assert.False(viewModel.IsError);
        Assert.Contains("canceled", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FixedCertificateFilePicker(string? filePath) : ICertificateFilePickerService
    {
        public string? PickCertificateFile() => filePath;
    }

    private sealed class FakeCertificateInspectorService : ICertificateInspectorService
    {
        public Exception? StoreError { get; init; }
        public CertificateStoreScope? LastStoreScope { get; private set; }
        public string? LastInspectedThumbprint { get; private set; }

        public Task<CertificateInspectionResult> InspectFileAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateInspection(filePath));

        public Task<IReadOnlyList<CertificateListItem>> GetStoreCertificatesAsync(
            CertificateStoreScope scope,
            CancellationToken cancellationToken = default)
        {
            LastStoreScope = scope;
            if (StoreError is not null)
            {
                return Task.FromException<IReadOnlyList<CertificateListItem>>(StoreError);
            }

            return Task.FromResult<IReadOnlyList<CertificateListItem>>(
                [new CertificateListItem("thumbprint", "subject", "issuer", DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1))]);
        }

        public Task<CertificateInspectionResult> InspectStoreCertificateAsync(
            CertificateStoreScope scope,
            string thumbprint,
            CancellationToken cancellationToken = default)
        {
            LastStoreScope = scope;
            LastInspectedThumbprint = thumbprint;
            return Task.FromResult(CreateInspection("Windows Current User personal store"));
        }

        private static CertificateInspectionResult CreateInspection(string source) =>
            new(
                source,
                "subject",
                "issuer",
                "thumbprint",
                DateTimeOffset.Now,
                DateTimeOffset.Now.AddDays(1),
                true,
                "Within validity dates",
                "Not trusted by local Windows chain policy",
                [],
                [],
                "Not checked (network revocation checks are disabled).",
                [],
                [],
                "RSA",
                "SHA256");
    }
}
