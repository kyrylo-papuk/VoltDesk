using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VoltDesk.Desktop.ViewModels;

namespace VoltDesk.Desktop.Views;

/// <summary>
/// PasswordBox.Password is not bindable by design (it is not stored in a dependency property),
/// so the view copies it to the view model and back.
/// </summary>
public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private LoginViewModel? ViewModel => DataContext as LoginViewModel;

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && vm.Password != PasswordInput.Password)
        {
            vm.Password = PasswordInput.Password;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged old)
        {
            old.PropertyChanged -= OnViewModelChanged;
        }

        if (e.NewValue is INotifyPropertyChanged current)
        {
            current.PropertyChanged += OnViewModelChanged;
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoginViewModel.Password) && ViewModel is { } vm && vm.Password != PasswordInput.Password)
        {
            PasswordInput.Password = vm.Password;
        }
    }
}
