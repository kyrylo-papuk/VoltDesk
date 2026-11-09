using VoltDesk.Core.Common;
using VoltDesk.Core.Employees;

namespace VoltDesk.Core.Projects;

public sealed class ProjectTask : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const decimal MaxEstimatedHours = 1000;

    private ProjectTask()
    {
    }

    public int ProjectId { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public Discipline Discipline { get; private set; }

    /// <summary>Design stage the task belongs to. A stage cannot be closed while its tasks are open.</summary>
    public DesignStage Stage { get; private set; }

    public ProjectTaskStatus Status { get; private set; }

    public TaskPriority Priority { get; private set; }

    public int? AssigneeId { get; private set; }

    public Employee? Assignee { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public decimal? EstimatedHours { get; private set; }

    public DateOnly? CompletedOn { get; private set; }

    public bool IsDone => Status == ProjectTaskStatus.Done;

    private bool HasAssignee => Assignee is not null || AssigneeId is not null;

    internal static ProjectTask Create(
        DesignStage stage,
        string title,
        Discipline discipline,
        TaskPriority priority,
        DateOnly? dueDate,
        decimal? estimatedHours)
    {
        var task = new ProjectTask { Stage = stage, Status = ProjectTaskStatus.ToDo };
        task.UpdateDetails(title, description: null, discipline, priority, dueDate, estimatedHours);
        return task;
    }

    public void UpdateDetails(
        string title,
        string? description,
        Discipline discipline,
        TaskPriority priority,
        DateOnly? dueDate,
        decimal? estimatedHours)
    {
        if (estimatedHours is <= 0 or > MaxEstimatedHours)
        {
            throw new DomainException($"Estimated hours must be between 0 and {MaxEstimatedHours}.");
        }

        Title = Require.Text(title, "Task title", TitleMaxLength);
        Description = Require.OptionalText(description, "Task description", DescriptionMaxLength);
        Discipline = Require.Defined(discipline, "Discipline");
        Priority = Require.Defined(priority, "Priority");
        DueDate = dueDate;
        EstimatedHours = estimatedHours;
    }

    public void AssignTo(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);
        if (!employee.IsActive)
        {
            throw new DomainException($"{employee.FullName} is no longer active.");
        }

        Assignee = employee;
    }

    public void Unassign()
    {
        if (Status != ProjectTaskStatus.ToDo)
        {
            throw new DomainException("Only tasks in To Do can be unassigned.");
        }

        Assignee = null;
        AssigneeId = null;
    }

    public void MoveTo(ProjectTaskStatus status, DateOnly today)
    {
        Require.Defined(status, "Task status");
        if (status == Status)
        {
            return;
        }

        if (status != ProjectTaskStatus.ToDo && !HasAssignee)
        {
            throw new DomainException("Assign the task to someone before work on it starts.");
        }

        Status = status;
        CompletedOn = status == ProjectTaskStatus.Done ? today : null;
    }
}
