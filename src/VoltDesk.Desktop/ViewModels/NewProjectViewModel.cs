using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoltDesk.Contracts;
using VoltDesk.Core.Common;
using VoltDesk.Core.Employees;
using VoltDesk.Core.Projects;
using VoltDesk.Desktop.Services;

namespace VoltDesk.Desktop.ViewModels;

/// <summary>"New project" dialog. Raises <see cref="Created"/> when the server accepted the project.</summary>
public sealed partial class NewProjectViewModel(ApiClient api) : PageViewModel
{
    public override string Section => "Projects";

    public event EventHandler<ProjectDto>? Created;

    public List<ClientSummaryDto> Clients { get; private set; } = [];

    public List<EmployeeDto> Managers { get; private set; } = [];

    public IReadOnlyList<BuildingType> BuildingTypes { get; } = Enum.GetValues<BuildingType>();

    public IReadOnlyList<string> States { get; } = [.. UsStates.All.Order()];

    [ObservableProperty]
    public partial string Number { get; set; } = $"{DateTime.Today.Year}-";

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ClientSummaryDto? Client { get; set; }

    [ObservableProperty]
    public partial BuildingType BuildingType { get; set; } = BuildingType.Commercial;

    [ObservableProperty]
    public partial string City { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? State { get; set; }

    [ObservableProperty]
    public partial EmployeeDto? Manager { get; set; }

    [ObservableProperty]
    public partial decimal Fee { get; set; }

    public override Task LoadAsync() => RunAsync(async () =>
    {
        Clients = await api.GetClientsAsync(search: null);
        Managers = [.. (await api.GetEmployeesAsync()).Where(e => e.Role is EmployeeRole.ProjectManager or EmployeeRole.Admin)];
        OnPropertyChanged(nameof(Clients));
        OnPropertyChanged(nameof(Managers));
    });

    [RelayCommand]
    private Task CreateAsync() => RunAsync(async () =>
    {
        if (Client is null || Manager is null || State is null)
        {
            ErrorMessage = "Choose a client, a state and a project manager.";
            return;
        }

        var project = await api.CreateProjectAsync(new CreateProjectRequest(
            Number.Trim(), Name.Trim(), Client.Id, BuildingType, City.Trim(), State, Manager.Id, Fee));
        Created?.Invoke(this, project);
    });
}
