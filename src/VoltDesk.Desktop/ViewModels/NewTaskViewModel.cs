using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoltDesk.Contracts;
using VoltDesk.Core.Projects;
using VoltDesk.Desktop.Services;

namespace VoltDesk.Desktop.ViewModels;

/// <summary>"New task" dialog. The task joins the current design stage of the chosen project.</summary>
public sealed partial class NewTaskViewModel(ApiClient api) : PageViewModel
{
    public override string Section => "Tasks";

    public event EventHandler<TaskDto>? Created;

    public List<ProjectSummaryDto> Projects { get; private set; } = [];

    public IReadOnlyList<Discipline> Disciplines { get; } = Enum.GetValues<Discipline>();

    public IReadOnlyList<TaskPriority> Priorities { get; } = Enum.GetValues<TaskPriority>();

    [ObservableProperty]
    public partial ProjectSummaryDto? Project { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Discipline Discipline { get; set; } = Discipline.Power;

    [ObservableProperty]
    public partial TaskPriority Priority { get; set; } = TaskPriority.Normal;

    [ObservableProperty]
    public partial DateOnly? DueDate { get; set; }

    [ObservableProperty]
    public partial decimal? EstimatedHours { get; set; }

    public Task LoadAsync(int? projectId) => RunAsync(async () =>
    {
        Projects = await api.GetProjectsAsync(search: null, ProjectStatus.Active);
        OnPropertyChanged(nameof(Projects));
        Project = Projects.FirstOrDefault(p => p.Id == projectId);
    });

    [RelayCommand]
    private Task CreateAsync() => RunAsync(async () =>
    {
        if (Project is null)
        {
            ErrorMessage = "Choose a project.";
            return;
        }

        var task = await api.CreateTaskAsync(Project.Id, new CreateTaskRequest(
            Title.Trim(),
            string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Discipline,
            Priority,
            DueDate,
            EstimatedHours));
        Created?.Invoke(this, task);
    });
}
