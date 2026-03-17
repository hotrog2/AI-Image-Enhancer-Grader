using CommunityToolkit.Mvvm.ComponentModel;

namespace ColorGrader.App.ViewModels;

public partial class ExportQueueItemViewModel(string fileName) : ObservableObject
{
    public string FileName { get; } = fileName;

    [ObservableProperty]
    private string status = "Queued";

    [ObservableProperty]
    private string detail = string.Empty;
}
