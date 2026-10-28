namespace TasklyDev.Services;

/// <summary>Provides a file-selection dialog for local input files.</summary>
public interface IFilePickerService
{
    /// <summary>Prompts the user to select a file.</summary>
    /// <returns>The selected path, or <see langword="null"/> when canceled.</returns>
    string? PickFile();
}
