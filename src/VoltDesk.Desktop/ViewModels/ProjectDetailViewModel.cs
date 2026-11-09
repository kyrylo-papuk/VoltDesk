using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoltDesk.Contracts;
using VoltDesk.Core.Projects;
using VoltDesk.Desktop.Converters;
using VoltDesk.Desktop.Services;

namespace VoltDesk.Desktop.ViewModels;

/// <summary>One design stage on the timeline, with an editable due date.</summary>
public sealed partial class MilestoneRow(MilestoneDto milestone, DesignStage currentStage, bool editable) : ObservableObject
{
    public MilestoneDto Milestone { get; } = milestone;

    public bool IsCurrent => Milestone.Stage == currentStage && Milestone.CompletedOn is null;

    public bool IsEditable { get; } = editable && milestone.CompletedOn is null;

    public string State => Milestone switch
    {
        { CompletedOn: not null } => "Done",
        { IsOverdue: true } => "Overdue",
        _ when IsCurrent => "Current",
        _ => "Upcoming",
    };

    public string StateText => Milestone.CompletedOn is { } done ? $"Closed {done:MMM d, yyyy}" : State;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged))]
    public partial DateOnly? DueDate { get; set; } = milestone.DueDate;

    public bool IsChanged => DueDate is not null && DueDate != Milestone.DueDate;
}

public sealed record TaskGroup(string Title, IReadOnlyList<TaskDto> Tasks);

public sealed record HistoryRow(AuditEntryDto Entry)
{
    public string When => Entry.OccurredAt.ToLocalTime().ToString("MMM d, HH:mm");

    public string Who => Entry.ActorName ?? "System";

    public string What => $"{Entry.Action} {EnumDisplayConverter.Humanize(Entry.EntityType).ToLowerInvariant()}";

    public string Details => string.Join(
        Environment.NewLine,
        Entry.Changes.Select(c => $"{c.Key}: {c.Value.Old ?? "—"} → {c.Value.New ?? "—"}"));
}

public sealed partial class ProjectDetailViewModel(ApiClient api, Session session, Navigator navigator) : PageViewModel
{
    public override string Section => "Projects";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdvance), nameof(CanHold), nameof(CanResume), nameof(CanCancel), nameof(Subtitle))]
    public partial ProjectDto? Project { get; set; }

    public ObservableCollection<MilestoneRow> Milestones { get; } = [];

    public ObservableCollection<TaskGroup> TaskGroups { get; } = [];

    public ObservableCollection<HistoryRow> History { get; } = [];

    public bool CanManage => session.CanManageProjects;

    public bool CanAdvance => CanManage && Project?.Status == ProjectStatus.Active;

    public bool CanHold => CanManage && Project?.Status == ProjectStatus.Active;

    public bool CanResume => CanManage && Project?.Status == ProjectStatus.OnHold;

    public bool CanCancel => CanManage && Project?.Status is ProjectStatus.Active or ProjectStatus.OnHold;

    public string Subtitle => Project is null
        ? string.Empty
        : $"{Project.ClientName} · {Project.City}, {Project.State} · " +
          $"{EnumDisplayConverter.Humanize(Project.BuildingType.ToString())} · PM {Project.ProjectManagerName} · {Project.Fee:$#,0}";

    public Task LoadAsync(int projectId) => RunAsync(() => ReloadAsync(projectId));

    public override Task LoadAsync() => Project is null ? Task.CompletedTask : LoadAsync(Project.Id);

    [RelayCommand]
    private Task BackAsync() => navigator.GoBackAsync();

    [RelayCommand]
    private Task AdvanceAsync() => ChangeStateAsync("advance", "Stage closed.");

    [RelayCommand]
    private Task HoldAsync() => ChangeStateAsync("hold", "Project put on hold.");

    [RelayCommand]
    private Task ResumeAsync() => ChangeStateAsync("resume", "Project resumed.");

    [RelayCommand]
    private Task CancelProjectAsync() => ChangeStateAsync("cancel", "Project cancelled.");

    [RelayCommand]
    private Task SaveMilestoneAsync(MilestoneRow row) => RunAsync(
        async () =>
        {
            var updated = await api.ScheduleMilestoneAsync(Project!.Id, row.Milestone.Stage, row.DueDate!.Value, Project.Version);
            await ShowAsync(updated);
            InfoMessage = $"{row.Milestone.Stage.DisplayName()} is now due {row.DueDate:MMM d, yyyy}.";
        },
        onConflict: () => ReloadAsync(Project!.Id));

    private Task ChangeStateAsync(string action, string done) => RunAsync(
        async () =>
        {
            var updated = await api.ChangeProjectStateAsync(Project!.Id, action, Project.Version);
            await ShowAsync(updated);
            InfoMessage = done;
        },
        onConflict: () => ReloadAsync(Project!.Id));

    private async Task ReloadAsync(int projectId) => await ShowAsync(await api.GetProjectAsync(projectId));

    private async Task ShowAsync(ProjectDto project)
    {
        Project = project;
        var editable = CanManage && project.Status is ProjectStatus.Active or ProjectStatus.OnHold;

        Milestones.Clear();
        foreach (var milestone in project.Milestones)
        {
            Milestones.Add(new MilestoneRow(milestone, project.Stage, editable));
        }

        TaskGroups.Clear();
        foreach (var group in project.Tasks.GroupBy(t => t.Stage).OrderByDescending(g => g.Key))
        {
            var open = group.Count(t => t.Status != ProjectTaskStatus.Done);
            TaskGroups.Add(new TaskGroup($"{group.Key.DisplayName()} · {open} open of {group.Count()}", [.. group]));
        }

        History.Clear();
        foreach (var entry in await api.GetProjectHistoryAsync(project.Id))
        {
            History.Add(new HistoryRow(entry));
        }
    }
}
