namespace TasklyDev.Services;

/// <summary>Selects supported public-certificate files for local inspection.</summary>
public interface ICertificateFilePickerService
{
    /// <summary>Prompts for a public certificate file and returns null when canceled.</summary>
    string? PickCertificateFile();
}
