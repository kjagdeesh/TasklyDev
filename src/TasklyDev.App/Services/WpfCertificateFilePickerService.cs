using Microsoft.Win32;

namespace TasklyDev.Services;

/// <summary>Displays a filtered Windows file-open dialog for public certificates.</summary>
public sealed class WpfCertificateFilePickerService : ICertificateFilePickerService
{
    /// <inheritdoc />
    public string? PickCertificateFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a public certificate",
            Filter = "Public certificates (*.cer;*.crt;*.der;*.pem)|*.cer;*.crt;*.der;*.pem",
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
