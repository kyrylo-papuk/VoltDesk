using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoltDesk.Contracts;
using VoltDesk.Core.Projects;
using VoltDesk.Desktop.Services;

namespace VoltDesk.Desktop.ViewModels;

public sealed record StatusOption(string Label, ProjectStatus? Status);

/// <summary>A project in the list, with values the grid shows directly.</summary>
public sealed record ProjectRow(ProjectSummaryDto Project, DateOnly Today)
{
    public bool IsStageOverdue => Project.Status == ProjectStatus.Active && Project.StageDueDate < Today;

    public string StageDueText => Project.StageDueDate?.ToString("MMM d, yyyy") ?? "—";

    public string FeeText => Project.Fee.ToString("$#,0");
}

public sealed partial class ProjectsViewModel(
    ApiClient api,
    Session session,
    Navigator navigator,
    DialogService dialogs) : PageViewModel
{
    private CancellationTokenSource? _searchDelay;

    public override string Section => "Projects";

    public bool CanManage => session.CanManageProjects;

    public ObservableCollection<ProjectRow> Projects { get; } = [];

    private static readonly StatusOption[] AllStatusOptions =
    [
        new("All statuses", null),
        new("Active", ProjectStatus.Active),
        new("On hold", ProjectStatus.OnHold),
        new("Completed", ProjectStatus.Completed),
        new("Cancelled", ProjectStatus.Cancelled),
    ];

    public IReadOnlyList<StatusOption> StatusOptions => AllStatusOptions;

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    [ObservableProperty]
    public partial StatusOption? StatusFilter { get; set; } = AllStatusOptions[0];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenSelectedCommand))]
    public partial ProjectRow? SelectedProject { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    public override Task LoadAsync() => RunAsync(ReloadAsync);

    partial void OnStatusFilterChanged(StatusOption? value) => _ = LoadAsync();

    // Waits for a pause in typing before asking the server.
    partial void OnSearchChanged(string value)
    {
        _searchDelay?.Cancel();
        _searchDelay = new CancellationTokenSource();
        var token = _searchDelay.Token;
        _ = Task.Delay(300, token).ContinueWith(
            _ => LoadAsync(),
            token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    [RelayCommand]
    private Task OpenAsync(ProjectRow? row) =>
        row is null ? Task.CompletedTask : navigator.OpenAsync<ProjectDetailViewModel>(page => page.LoadAsync(row.Project.Id));

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task OpenSelectedAsync() => OpenAsync(SelectedProject);

    private bool HasSelection() => SelectedProject is not null;

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        var created = await dialogs.ShowNewProjectAsync();
        if (created is not null)
        {
            await navigator.OpenAsync<ProjectDetailViewModel>(page => page.LoadAsync(created.Id));
        }
    }

    private async Task ReloadAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var projects = await api.GetProjectsAsync(Search, StatusFilter?.Status);

        Projects.Clear();
        foreach (var project in projects)
        {
            Projects.Add(new ProjectRow(project, today));
        }

        var active = Projects.Count(p => p.Project.Status == ProjectStatus.Active);
        var overdue = Projects.Count(p => p.IsStageOverdue);
        Summary = $"{Projects.Count} projects · {active} active · {overdue} with an overdue stage";
    }
}
