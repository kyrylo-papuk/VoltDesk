using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VoltDesk.Api.Auth;
using VoltDesk.Contracts;
using VoltDesk.Core.Projects;
using VoltDesk.Infrastructure.Integrations.Google;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Api.Controllers;

[ApiController]
public sealed class IntegrationsController(
    VoltDeskDbContext db,
    IGoogleWorkspace google,
    GoogleFolders folders,
    IOptions<GoogleOptions> options,
    TimeProvider clock) : ControllerBase
{
    /// <summary>Whether Google sync is on and how much is waiting or has failed.</summary>
    [HttpGet("api/integrations/google")]
    public async Task<GoogleStatusDto> Status(CancellationToken cancellationToken) => new(
        options.Value.Enabled,
        await db.Outbox.CountAsync(m => m.ProcessedAt == null && m.FailedAt == null, cancellationToken),
        await db.Outbox.CountAsync(m => m.FailedAt != null, cancellationToken));

    /// <summary>Writes everyone's open work to a new Google Sheet in the VoltDesk Drive folder.</summary>
    [HttpPost("api/reports/workload/google-sheets")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<ExternalFileDto> WorkloadToSheets(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var openTasks = await (
                from task in db.ProjectTasks.AsNoTracking()
                join project in db.Projects on task.ProjectId equals project.Id
                where project.Status == ProjectStatus.Active && task.Status != ProjectTaskStatus.Done && task.AssigneeId != null
                select new { task.AssigneeId, task.DueDate, task.EstimatedHours, project.Number })
            .ToListAsync(cancellationToken);
        var employees = await db.Employees.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync(cancellationToken);

        List<IReadOnlyList<object>> rows =
        [
            ["Employee", "Role", "Open tasks", "Overdue", "Estimated hours", "Projects"],
        ];
        foreach (var employee in employees)
        {
            var mine = openTasks.Where(t => t.AssigneeId == employee.Id).ToList();
            rows.Add(
            [
                employee.FullName,
                employee.Role.ToString(),
                mine.Count,
                mine.Count(t => t.DueDate < today),
                mine.Sum(t => t.EstimatedHours ?? 0),
                string.Join(", ", mine.Select(t => t.Number).Distinct().Order()),
            ]);
        }

        var root = await folders.EnsureRootAsync(cancellationToken);
        var title = $"VoltDesk workload {today:yyyy-MM-dd}";
        var sheet = await google.CreateSpreadsheetAsync(title, root.ExternalId, rows, cancellationToken);
        return new ExternalFileDto(title, sheet.Url);
    }
}
