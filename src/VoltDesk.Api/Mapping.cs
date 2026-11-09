using VoltDesk.Contracts;
using VoltDesk.Core.Clients;
using VoltDesk.Core.Employees;
using VoltDesk.Core.Projects;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Api;

/// <summary>Maps tracked entities to DTOs. Versions come from the rowversion shadow property.</summary>
internal static class Mapping
{
    public static EmployeeDto ToDto(this Employee employee, VoltDeskDbContext db) =>
        new(employee.Id, employee.FullName, employee.Email, employee.Role, employee.IsActive, db.Entry(employee).Version());

    public static ClientDto ToDto(this Client client, VoltDeskDbContext db) => new(
        client.Id,
        client.Name,
        [.. client.Contacts
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.FullName)
            .Select(c => new ContactDto(c.Id, c.FullName, c.Title, c.Email, c.Phone, c.IsPrimary))],
        db.Entry(client).Version());

    public static ProjectDto ToDto(this Project project, VoltDeskDbContext db, DateOnly today) => new(
        project.Id,
        project.Number,
        project.Name,
        project.ClientId,
        project.Client.Name,
        project.BuildingType,
        project.City,
        project.State,
        project.Stage,
        project.Status,
        project.ProjectManagerId,
        project.ProjectManager.FullName,
        project.Fee,
        [.. project.Milestones.Select(m => new MilestoneDto(m.Stage, m.DueDate, m.CompletedOn, m.IsOverdue(today)))],
        [.. project.Tasks
            .OrderBy(t => t.Stage)
            .ThenBy(t => t.Status)
            .ThenByDescending(t => t.Priority)
            .Select(t => t.ToDto(db, project.Number))],
        db.Entry(project).Version());

    public static TaskDto ToDto(this ProjectTask task, VoltDeskDbContext db, string projectNumber) => new(
        task.Id,
        task.ProjectId,
        projectNumber,
        task.Title,
        task.Description,
        task.Discipline,
        task.Stage,
        task.Status,
        task.Priority,
        task.AssigneeId,
        task.Assignee?.FullName,
        task.DueDate,
        task.EstimatedHours,
        task.CompletedOn,
        db.Entry(task).Version());
}
