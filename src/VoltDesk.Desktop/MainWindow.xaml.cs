using System.Windows;
using VoltDesk.Desktop.ViewModels;

namespace VoltDesk.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.CheckConnectionCommand.Execute(null);
    }
}
