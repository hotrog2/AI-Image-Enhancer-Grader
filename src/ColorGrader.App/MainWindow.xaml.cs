using System.Windows;
using ColorGrader.App.ViewModels;

namespace ColorGrader.App;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeCommand.ExecuteAsync(null);
    }
}
