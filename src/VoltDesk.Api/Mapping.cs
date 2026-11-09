using VoltDesk.Contracts;
using VoltDesk.Core.Clients;
using VoltDesk.Core.Employees;
using VoltDesk.Core.Panels;
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

    public static ProjectDto ToDto(this Project project, VoltDeskDbContext db, DateOnly today, string? documentsFolderUrl) => new(
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
        db.Entry(project).Version(),
        documentsFolderUrl);

    public static PanelDto ToDto(this ElectricalPanel panel, VoltDeskDbContext db, string projectNumber)
    {
        var calculation = panel.Calculate();
        return new PanelDto(
            panel.Id,
            panel.ProjectId,
            projectNumber,
            panel.Name,
            panel.Location,
            panel.System,
            panel.BusRatingAmps,
            panel.MainBreakerAmps,
            panel.Spaces,
            [.. calculation.Circuits.Select(load => new CircuitDto(
                load.Circuit.Id,
                load.Circuit.Slot,
                load.Circuit.Slots,
                load.Circuit.Label,
                load.Circuit.Description,
                load.Circuit.LoadType,
                load.Circuit.LoadVa,
                load.Circuit.Poles,
                load.Circuit.BreakerAmps,
                Math.Round(load.Amps, 1),
                load.BreakerTooSmall))],
            calculation.ToDto(),
            db.Entry(panel).Version());
    }

    public static PanelSummaryDto ToSummaryDto(this ElectricalPanel panel)
    {
        var calculation = panel.Calculate();
        return new PanelSummaryDto(
            panel.Id,
            panel.ProjectId,
            panel.Name,
            panel.Location,
            panel.System,
            panel.BusRatingAmps,
            panel.Circuits.Count,
            Math.Round(calculation.TotalKva, 1),
            Math.Round(calculation.ImbalancePercent, 1),
            calculation.Warnings.Count);
    }

    public static PanelCalculationDto ToDto(this PanelCalculation calculation) => new(
        [.. calculation.Phases.Select(p => new PhaseLoadDto(p.Phase, Math.Round(p.Va, 0), Math.Round(p.Amps, 1)))],
        Math.Round(calculation.TotalVa, 0),
        Math.Round(calculation.BalancedAmps, 1),
        Math.Round(calculation.ImbalancePercent, 1),
        Math.Round(calculation.BusUtilizationPercent, 1),
        calculation.Warnings);

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
