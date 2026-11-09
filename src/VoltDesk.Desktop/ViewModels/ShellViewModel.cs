using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltDesk.Desktop.Services;

namespace VoltDesk.Desktop.ViewModels;

/// <summary>Main window: sidebar navigation around the current page.</summary>
public sealed partial class ShellViewModel : ObservableObject, IRecipient<SessionExpiredMessage>
{
    public ShellViewModel(Navigator navigator, Session session)
    {
        Navigator = navigator;
        Session = session;
        WeakReferenceMessenger.Default.Register(this);
    }

    public Navigator Navigator { get; }

    public Session Session { get; }

    public Task StartAsync() => Navigator.OpenSectionAsync<LoginViewModel>();

    public async void Receive(SessionExpiredMessage message)
    {
        Session.End();
        await Navigator.OpenSectionAsync<LoginViewModel>();
        Navigator.Current!.ErrorMessage = message.Reason;
    }

    [RelayCommand]
    private Task ShowProjectsAsync() => Navigator.OpenSectionAsync<ProjectsViewModel>();

    [RelayCommand]
    private Task ShowTasksAsync() => Navigator.OpenSectionAsync<TaskBoardViewModel>();

    [RelayCommand]
    private Task ShowClientsAsync() => Navigator.OpenSectionAsync<ClientsViewModel>();

    [RelayCommand]
    private Task SignOutAsync()
    {
        Session.End();
        return Navigator.OpenSectionAsync<LoginViewModel>();
    }
}
