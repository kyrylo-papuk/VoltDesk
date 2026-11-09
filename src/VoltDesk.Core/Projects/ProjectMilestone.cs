namespace VoltDesk.Core.Projects;

/// <summary>Due date and completion of one design stage. Every project has one per stage.</summary>
public sealed class ProjectMilestone
{
    private ProjectMilestone()
    {
    }

    internal ProjectMilestone(DesignStage stage) => Stage = stage;

    public DesignStage Stage { get; private set; }

    public DateOnly? DueDate { get; internal set; }

    public DateOnly? CompletedOn { get; internal set; }

    public bool IsOverdue(DateOnly today) => CompletedOn is null && DueDate < today;
}
