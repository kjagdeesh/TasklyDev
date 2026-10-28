using Microsoft.Win32;

namespace TasklyDev.Services;

/// <summary>Displays the Windows file-open dialog for selecting a single file to hash.</summary>
public sealed class WpfFilePickerService : IFilePickerService
{
    /// <inheritdoc />
    public string? PickFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a file to hash",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
