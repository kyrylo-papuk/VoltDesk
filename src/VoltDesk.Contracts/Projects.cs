using System.ComponentModel.DataAnnotations;
using VoltDesk.Core.Projects;

namespace VoltDesk.Contracts;

public sealed record ProjectSummaryDto(
    int Id,
    string Number,
    string Name,
    int ClientId,
    string ClientName,
    BuildingType BuildingType,
    string City,
    string State,
    DesignStage Stage,
    ProjectStatus Status,
    int ProjectManagerId,
    string ProjectManagerName,
    decimal Fee,
    int OpenTasks,
    DateOnly? StageDueDate);

public sealed record ProjectDto(
    int Id,
    string Number,
    string Name,
    int ClientId,
    string ClientName,
    BuildingType BuildingType,
    string City,
    string State,
    DesignStage Stage,
    ProjectStatus Status,
    int ProjectManagerId,
    string ProjectManagerName,
    decimal Fee,
    IReadOnlyList<MilestoneDto> Milestones,
    IReadOnlyList<TaskDto> Tasks,
    string Version);

public sealed record MilestoneDto(DesignStage Stage, DateOnly? DueDate, DateOnly? CompletedOn, bool IsOverdue);

public sealed record CreateProjectRequest(
    [Required, StringLength(Project.NumberMaxLength)] string Number,
    [Required, StringLength(Project.NameMaxLength)] string Name,
    int ClientId,
    BuildingType BuildingType,
    [Required, StringLength(Project.CityMaxLength)] string City,
    [Required, StringLength(2, MinimumLength = 2)] string State,
    int ProjectManagerId,
    [Range(0, 100_000_000)] decimal Fee);

public sealed record UpdateProjectRequest(
    [Required, StringLength(Project.NameMaxLength)] string Name,
    BuildingType BuildingType,
    [Required, StringLength(Project.CityMaxLength)] string City,
    [Required, StringLength(2, MinimumLength = 2)] string State,
    [Range(0, 100_000_000)] decimal Fee,
    [Required] string Version);

public sealed record ChangeManagerRequest(int ProjectManagerId, [Required] string Version);

public sealed record ScheduleMilestoneRequest(DateOnly DueDate, [Required] string Version);

/// <summary>Body of state transitions (advance, hold, resume, cancel).</summary>
public sealed record VersionRequest([Required] string Version);
