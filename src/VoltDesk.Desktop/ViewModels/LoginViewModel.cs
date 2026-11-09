using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using VoltDesk.Desktop.Services;

namespace VoltDesk.Desktop.ViewModels;

public sealed class DemoOptions
{
    public const string Section = "Demo";

    public string? Password { get; set; }

    public List<DemoAccount> Accounts { get; set; } = [];
}

public sealed class DemoAccount
{
    public string Label { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

public sealed partial class LoginViewModel(
    ApiClient api,
    Session session,
    Navigator navigator,
    IOptions<DemoOptions> demo) : PageViewModel
{
    public override string Section => string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ApiHealth? ServerHealth { get; set; }

    public IReadOnlyList<DemoAccount> DemoAccounts => demo.Value.Accounts;

    public bool HasDemoAccounts => DemoAccounts.Count > 0 && demo.Value.Password is not null;

    public string ServerStatus => ServerHealth switch
    {
        ApiHealth.Healthy => "Server online",
        ApiHealth.DatabaseUnavailable => "Server online, database unavailable",
        ApiHealth.Unreachable => "Server unreachable",
        _ => "Checking server…",
    };

    public override async Task LoadAsync()
    {
        ServerHealth = await api.GetHealthAsync();
        OnPropertyChanged(nameof(ServerStatus));
    }

    // Handles its own errors: here a 401 means "wrong password", not "session expired".
    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var login = await api.LoginAsync(Email.Trim(), Password);
            session.Start(login);
            Password = string.Empty;
            await navigator.OpenSectionAsync<ProjectsViewModel>();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSignIn() => !string.IsNullOrWhiteSpace(Email) && Password.Length > 0;

    [RelayCommand]
    private Task UseDemoAccountAsync(DemoAccount account)
    {
        Email = account.Email;
        Password = demo.Value.Password ?? string.Empty;
        return SignInAsync();
    }
}
