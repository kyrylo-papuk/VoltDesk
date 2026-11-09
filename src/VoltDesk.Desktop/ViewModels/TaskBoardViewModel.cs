using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoltDesk.Contracts;
using VoltDesk.Core.Projects;
using VoltDesk.Desktop.Services;

namespace VoltDesk.Desktop.ViewModels;

public sealed record FilterOption(string Label, int? Id);

/// <summary>A card on the board. Replaced task data keeps the same card instance.</summary>
public sealed partial class TaskCard(TaskDto task) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initials), nameof(IsOverdue), nameof(DueText), nameof(IsHighPriority), nameof(AutomationKey))]
    public partial TaskDto Task { get; set; } = task;

    public string Initials => Task.AssigneeName is { } name
        ? string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => part[0]))
        : "?";

    public bool IsOverdue => Task.Status != ProjectTaskStatus.Done && Task.DueDate < DateOnly.FromDateTime(DateTime.Today);

    public string DueText => Task.DueDate is { } due ? due.ToString("MMM d") : "No due date";

    public bool IsHighPriority => Task.Priority == TaskPriority.High;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Stable id for UI automation: "Task 12".</summary>
    public string AutomationKey => $"Task {Task.Id}";
}

public sealed partial class BoardColumn(ProjectTaskStatus status, string title) : ObservableObject
{
    public ProjectTaskStatus Status { get; } = status;

    public string Title { get; } = title;

    public ObservableCollection<TaskCard> Cards { get; } = [];
}

/// <summary>Kanban board of tasks in active projects. Moves are optimistic and roll back on errors.</summary>
public sealed partial class TaskBoardViewModel(ApiClient api, Session session, DialogService dialogs) : PageViewModel
{
    private bool _loadingFilters;

    public override string Section => "Tasks";

    public bool CanManage => session.CanManageProjects;

    public bool CanExportWorkload => session.CanManageProjects && session.GoogleEnabled;

    public IReadOnlyList<BoardColumn> Columns { get; } =
    [
        new(ProjectTaskStatus.ToDo, "To do"),
        new(ProjectTaskStatus.InProgress, "In progress"),
        new(ProjectTaskStatus.InReview, "In review"),
        new(ProjectTaskStatus.Done, "Done"),
    ];

    public ObservableCollection<FilterOption> ProjectOptions { get; } = [];

    public ObservableCollection<FilterOption> AssigneeOptions { get; } = [];

    /// <summary>Assignee choices in the side panel; Id null means unassigned.</summary>
    public ObservableCollection<FilterOption> Assignees { get; } = [];

    public IReadOnlyList<TaskPriority> Priorities { get; } = Enum.GetValues<TaskPriority>();

    [ObservableProperty]
    public partial FilterOption? ProjectFilter { get; set; }

    [ObservableProperty]
    public partial FilterOption? AssigneeFilter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial TaskCard? SelectedCard { get; set; }

    [ObservableProperty]
    public partial FilterOption? EditAssignee { get; set; }

    [ObservableProperty]
    public partial TaskPriority EditPriority { get; set; }

    [ObservableProperty]
    public partial DateOnly? EditDueDate { get; set; }

    [ObservableProperty]
    public partial string EditTitle { get; set; } = string.Empty;

    public bool HasSelection => SelectedCard is not null;

    public override Task LoadAsync() => RunAsync(async () =>
    {
        await LoadFiltersAsync();
        await ReloadCardsAsync();
    });

    partial void OnProjectFilterChanged(FilterOption? value) => ReloadOnFilterChange();

    partial void OnAssigneeFilterChanged(FilterOption? value) => ReloadOnFilterChange();

    partial void OnSelectedCardChanged(TaskCard? oldValue, TaskCard? newValue)
    {
        oldValue?.IsSelected = false;
        if (newValue is not { } value)
        {
            return;
        }

        value.IsSelected = true;

        EditTitle = value.Task.Title;
        EditPriority = value.Task.Priority;
        EditDueDate = value.Task.DueDate;
        EditAssignee = Assignees.FirstOrDefault(a => a.Id == value.Task.AssigneeId);
    }

    [RelayCommand]
    private void Select(TaskCard card) => SelectedCard = card;

    [RelayCommand]
    private Task MoveLeftAsync(TaskCard card) => MoveByAsync(card, -1);

    [RelayCommand]
    private Task MoveRightAsync(TaskCard card) => MoveByAsync(card, +1);

    /// <summary>Moves a card to another column right away, then asks the server; undoes it on failure.</summary>
    public async Task MoveAsync(TaskCard card, ProjectTaskStatus target)
    {
        var from = Columns.Single(c => c.Cards.Contains(card));
        var to = Columns.Single(c => c.Status == target);
        if (from == to)
        {
            return;
        }

        from.Cards.Remove(card);
        to.Cards.Insert(0, card);
        var moved = false;
        await RunAsync(
            async () =>
            {
                card.Task = await api.MoveTaskAsync(card.Task.Id, target, card.Task.Version);
                moved = true;
            },
            onConflict: ReloadCardsAsync);

        // Rejected (e.g. no assignee): put the card back. After a conflict the board was reloaded instead.
        if (!moved && to.Cards.Remove(card))
        {
            from.Cards.Insert(0, card);
        }
    }

    [RelayCommand]
    private Task AssignToMeAsync() => SaveAssigneeAsync(session.User?.Id);

    [RelayCommand]
    private Task SaveAsync() => RunAsync(
        async () =>
        {
            var card = SelectedCard!;
            var task = card.Task;
            if (EditTitle != task.Title || EditPriority != task.Priority || EditDueDate != task.DueDate)
            {
                task = await api.UpdateTaskAsync(task.Id, new UpdateTaskRequest(
                    EditTitle, task.Description, task.Discipline, EditPriority, EditDueDate, task.EstimatedHours, task.Version));
            }

            if (EditAssignee?.Id != task.AssigneeId)
            {
                task = await api.AssignTaskAsync(task.Id, EditAssignee?.Id, task.Version);
            }

            card.Task = task;
            InfoMessage = "Task saved.";
        },
        onConflict: ReloadCardsAsync);

    [RelayCommand]
    private async Task NewTaskAsync()
    {
        var created = await dialogs.ShowNewTaskAsync(ProjectFilter?.Id);
        if (created is not null)
        {
            await RunAsync(ReloadCardsAsync);
            SelectedCard = Columns.SelectMany(c => c.Cards).FirstOrDefault(c => c.Task.Id == created.Id);
        }
    }

    [RelayCommand]
    private Task ExportWorkloadAsync() => RunAsync(async () =>
    {
        var sheet = await api.ExportWorkloadToSheetsAsync();
        InfoMessage = $"Workload report \"{sheet.Name}\" created in Google Sheets.";
        Browser.Open(sheet.Url);
    });

    private Task SaveAssigneeAsync(int? assigneeId) => RunAsync(
        async () =>
        {
            var card = SelectedCard!;
            card.Task = await api.AssignTaskAsync(card.Task.Id, assigneeId, card.Task.Version);
            EditAssignee = Assignees.FirstOrDefault(a => a.Id == card.Task.AssigneeId);
            InfoMessage = $"Assigned to {card.Task.AssigneeName ?? "nobody"}.";
        },
        onConflict: ReloadCardsAsync);

    private Task MoveByAsync(TaskCard card, int step)
    {
        var index = Columns.ToList().FindIndex(c => c.Cards.Contains(card)) + step;
        return index is >= 0 and < 4 ? MoveAsync(card, Columns[index].Status) : Task.CompletedTask;
    }

    private void ReloadOnFilterChange()
    {
        if (!_loadingFilters)
        {
            _ = RunAsync(ReloadCardsAsync);
        }
    }

    private async Task LoadFiltersAsync()
    {
        var projects = await api.GetProjectsAsync(search: null, ProjectStatus.Active);
        var employees = await api.GetEmployeesAsync();

        _loadingFilters = true;
        try
        {
            ProjectOptions.Clear();
            ProjectOptions.Add(new FilterOption("All active projects", null));
            foreach (var project in projects.OrderBy(p => p.Number))
            {
                ProjectOptions.Add(new FilterOption($"{project.Number} · {project.Name}", project.Id));
            }

            AssigneeOptions.Clear();
            AssigneeOptions.Add(new FilterOption("Everyone", null));
            AssigneeOptions.Add(new FilterOption("My tasks", session.User?.Id));

            Assignees.Clear();
            Assignees.Add(new FilterOption("Unassigned", null));
            foreach (var employee in employees)
            {
                Assignees.Add(new FilterOption(employee.FullName, employee.Id));
                AssigneeOptions.Add(new FilterOption(employee.FullName, employee.Id));
            }

            ProjectFilter = ProjectOptions[0];
            AssigneeFilter = AssigneeOptions[0];
        }
        finally
        {
            _loadingFilters = false;
        }
    }

    private async Task ReloadCardsAsync()
    {
        var selectedId = SelectedCard?.Task.Id;
        var tasks = await api.GetBoardTasksAsync(ProjectFilter?.Id, AssigneeFilter?.Id);

        foreach (var column in Columns)
        {
            column.Cards.Clear();
            var ordered = tasks.Where(t => t.Status == column.Status)
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.DueDate ?? DateOnly.MaxValue);
            foreach (var task in ordered)
            {
                column.Cards.Add(new TaskCard(task));
            }
        }

        SelectedCard = Columns.SelectMany(c => c.Cards).FirstOrDefault(c => c.Task.Id == selectedId);
    }
}
