using VoltDesk.Core.Clients;
using VoltDesk.Core.Common;
using VoltDesk.Core.Employees;

namespace VoltDesk.Core.Projects;

/// <summary>
/// A design contract for one building. Aggregate root for its milestones and tasks:
/// load it together with both collections before calling methods that change it.
/// </summary>
public sealed class Project : Entity
{
    public const int NumberMaxLength = 20;
    public const int NameMaxLength = 200;
    public const int CityMaxLength = 100;

    private readonly List<ProjectMilestone> _milestones = [];
    private readonly List<ProjectTask> _tasks = [];

    private Project()
    {
    }

    public string Number { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public int ClientId { get; private set; }

    public Client Client { get; private set; } = null!;

    public BuildingType BuildingType { get; private set; }

    public string City { get; private set; } = null!;

    /// <summary>Two-letter US state code.</summary>
    public string State { get; private set; } = null!;

    public int ProjectManagerId { get; private set; }

    public Employee ProjectManager { get; private set; } = null!;

    public DesignStage Stage { get; private set; }

    public ProjectStatus Status { get; private set; }

    /// <summary>Contract design fee, USD.</summary>
    public decimal Fee { get; private set; }

    /// <summary>Milestones in stage order (storage order is not guaranteed).</summary>
    public IReadOnlyList<ProjectMilestone> Milestones => [.. _milestones.OrderBy(m => m.Stage)];

    public IReadOnlyList<ProjectTask> Tasks => _tasks;

    public ProjectMilestone CurrentMilestone => Milestone(Stage);

    public static Project Create(
        string number,
        string name,
        Client client,
        BuildingType buildingType,
        string city,
        string state,
        Employee projectManager,
        decimal fee)
    {
        ArgumentNullException.ThrowIfNull(client);

        var project = new Project
        {
            Number = Require.Text(number, "Project number", NumberMaxLength).ToUpperInvariant(),
            Client = client,
            Stage = DesignStage.SchematicDesign,
            Status = ProjectStatus.Active,
        };
        project.UpdateDetails(name, buildingType, city, state, fee);
        project.AssignManager(projectManager);
        foreach (var stage in Enum.GetValues<DesignStage>())
        {
            project._milestones.Add(new ProjectMilestone(stage));
        }

        return project;
    }

    public void UpdateDetails(string name, BuildingType buildingType, string city, string state, decimal fee)
    {
        if (fee < 0)
        {
            throw new DomainException("Fee cannot be negative.");
        }

        Name = Require.Text(name, "Project name", NameMaxLength);
        BuildingType = Require.Defined(buildingType, "Building type");
        City = Require.Text(city, "City", CityMaxLength);
        State = UsStates.Normalize(state);
        Fee = fee;
    }

    public void AssignManager(Employee manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        if (!manager.CanManageProjects)
        {
            throw new DomainException($"{manager.FullName} cannot manage projects.");
        }

        ProjectManager = manager;
    }

    public ProjectMilestone Milestone(DesignStage stage) =>
        _milestones.SingleOrDefault(m => m.Stage == stage)
        ?? throw new DomainException($"Unknown design stage '{stage}'.");

    /// <summary>Sets a stage due date. Due dates must follow the stage order.</summary>
    public void ScheduleMilestone(DesignStage stage, DateOnly dueDate)
    {
        EnsureNotClosed();
        var milestone = Milestone(stage);
        if (milestone.CompletedOn is not null)
        {
            throw new DomainException($"{stage.DisplayName()} is already completed.");
        }

        var earlierConflict = _milestones.FirstOrDefault(m => m.Stage < stage && m.DueDate > dueDate);
        if (earlierConflict is not null)
        {
            throw new DomainException(
                $"{stage.DisplayName()} cannot be due before {earlierConflict.Stage.DisplayName()} ({earlierConflict.DueDate:yyyy-MM-dd}).");
        }

        var laterConflict = _milestones.FirstOrDefault(m => m.Stage > stage && m.DueDate < dueDate);
        if (laterConflict is not null)
        {
            throw new DomainException(
                $"{stage.DisplayName()} cannot be due after {laterConflict.Stage.DisplayName()} ({laterConflict.DueDate:yyyy-MM-dd}).");
        }

        milestone.DueDate = dueDate;
    }

    /// <summary>
    /// Closes the current stage and moves to the next one. Closing Construction Administration
    /// completes the project. A stage with open tasks cannot be closed.
    /// </summary>
    public void AdvanceStage(DateOnly today)
    {
        EnsureActive();
        var openTasks = _tasks.Count(t => t.Stage == Stage && !t.IsDone);
        if (openTasks > 0)
        {
            throw new DomainException($"{Stage.DisplayName()} still has {openTasks} open task(s).");
        }

        CurrentMilestone.CompletedOn = today;
        if (Stage == DesignStage.ConstructionAdministration)
        {
            Status = ProjectStatus.Completed;
            return;
        }

        Stage = (DesignStage)((int)Stage + 1);
    }

    public void PutOnHold()
    {
        EnsureActive();
        Status = ProjectStatus.OnHold;
    }

    public void Resume()
    {
        if (Status != ProjectStatus.OnHold)
        {
            throw new DomainException("Only a project on hold can be resumed.");
        }

        Status = ProjectStatus.Active;
    }

    public void Cancel()
    {
        EnsureNotClosed();
        Status = ProjectStatus.Cancelled;
    }

    /// <summary>Adds a task to the current design stage.</summary>
    public ProjectTask AddTask(
        string title,
        Discipline discipline,
        TaskPriority priority = TaskPriority.Normal,
        DateOnly? dueDate = null,
        decimal? estimatedHours = null,
        string? description = null)
    {
        EnsureActive();
        var task = ProjectTask.Create(Stage, title, description, discipline, priority, dueDate, estimatedHours);
        _tasks.Add(task);
        return task;
    }

    private void EnsureActive()
    {
        if (Status != ProjectStatus.Active)
        {
            throw new DomainException($"Project {Number} is {Status}, not active.");
        }
    }

    private void EnsureNotClosed()
    {
        if (Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
        {
            throw new DomainException($"Project {Number} is {Status}.");
        }
    }
}
