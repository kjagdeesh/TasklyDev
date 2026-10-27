using Microsoft.Win32;

namespace TasklyDev.Services;

public sealed class WpfFilePickerService : IFilePickerService
{
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
