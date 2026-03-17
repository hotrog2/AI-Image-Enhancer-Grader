using Microsoft.Win32;

namespace ColorGrader.App.Services;

public sealed class FolderPickerService : IFolderPickerService
{
    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Multiselect = false,
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
