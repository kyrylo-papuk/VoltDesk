using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoltDesk.Desktop.Services;

namespace VoltDesk.Desktop.ViewModels;

public sealed partial class MainViewModel(ApiClient api) : ObservableObject
{
    [ObservableProperty]
    public partial ApiHealth? Health { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Checking connection…";

    [RelayCommand]
    private async Task CheckConnectionAsync()
    {
        Health = null;
        StatusText = "Checking connection…";

        Health = await api.GetHealthAsync();
        StatusText = Health switch
        {
            ApiHealth.Healthy => "Connected to the server",
            ApiHealth.DatabaseUnavailable => "Server is up, but the database is unavailable",
            _ => "Server is unreachable",
        };
    }
}
