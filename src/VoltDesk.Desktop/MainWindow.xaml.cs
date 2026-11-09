using System.Windows;
using VoltDesk.Desktop.ViewModels;

namespace VoltDesk.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel shell)
    {
        InitializeComponent();
        DataContext = shell;
        Loaded += async (_, _) => await shell.StartAsync();
    }
}
