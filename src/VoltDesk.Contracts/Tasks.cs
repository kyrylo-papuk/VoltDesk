using System.ComponentModel.DataAnnotations;
using VoltDesk.Core.Projects;

namespace VoltDesk.Contracts;

public sealed record TaskDto(
    int Id,
    int ProjectId,
    string ProjectNumber,
    string Title,
    string? Description,
    Discipline Discipline,
    DesignStage Stage,
    ProjectTaskStatus Status,
    TaskPriority Priority,
    int? AssigneeId,
    string? AssigneeName,
    DateOnly? DueDate,
    decimal? EstimatedHours,
    DateOnly? CompletedOn,
    string Version);

public sealed record CreateTaskRequest(
    [Required, StringLength(ProjectTask.TitleMaxLength)] string Title,
    [StringLength(ProjectTask.DescriptionMaxLength)] string? Description,
    Discipline Discipline,
    TaskPriority Priority,
    DateOnly? DueDate,
    decimal? EstimatedHours);

public sealed record UpdateTaskRequest(
    [Required, StringLength(ProjectTask.TitleMaxLength)] string Title,
    [StringLength(ProjectTask.DescriptionMaxLength)] string? Description,
    Discipline Discipline,
    TaskPriority Priority,
    DateOnly? DueDate,
    decimal? EstimatedHours,
    [Required] string Version);

/// <summary>Assigns the task, or unassigns it when <see cref="AssigneeId"/> is null.</summary>
public sealed record AssignTaskRequest(int? AssigneeId, [Required] string Version);

public sealed record MoveTaskRequest(ProjectTaskStatus Status, [Required] string Version);
