using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltDesk.Api.Auth;
using VoltDesk.Contracts;
using VoltDesk.Core.Common;
using VoltDesk.Core.Projects;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Api.Controllers;

/// <summary>Task board operations. Any signed-in employee can work on tasks.</summary>
[ApiController]
[Route("api/tasks")]
public sealed class TasksController(VoltDeskDbContext db, TimeProvider clock) : ControllerBase
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    /// <param name="activeProjectsOnly">Skip tasks of projects on hold, completed or cancelled (the task board).</param>
    [HttpGet]
    public Task<List<TaskDto>> List(
        [FromQuery] int? projectId,
        [FromQuery] int? assigneeId,
        [FromQuery] ProjectTaskStatus? status,
        [FromQuery] bool activeProjectsOnly,
        CancellationToken cancellationToken) =>
        Query(projectId, assigneeId, status, activeProjectsOnly, openOnly: false).ToListAsync(cancellationToken);

    /// <summary>Open tasks of the signed-in employee in active projects.</summary>
    [HttpGet("mine")]
    public Task<List<TaskDto>> Mine(CancellationToken cancellationToken) =>
        Query(projectId: null, User.EmployeeId(), status: null, activeProjectsOnly: true, openOnly: true).ToListAsync(cancellationToken);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskDto>> Get(int id, CancellationToken cancellationToken)
    {
        var task = await Load(id, cancellationToken);
        return task is null ? NotFound() : task.ToDto(db, await ProjectNumber(task, cancellationToken));
    }

    [HttpPut("{id:int}")]
    public Task<ActionResult<TaskDto>> Update(int id, UpdateTaskRequest request, CancellationToken cancellationToken) =>
        Change(
            id,
            request.Version,
            task =>
            {
                task.UpdateDetails(
                    request.Title, request.Description, request.Discipline, request.Priority, request.DueDate, request.EstimatedHours);
                return Task.CompletedTask;
            },
            cancellationToken);

    [HttpPut("{id:int}/assignee")]
    public Task<ActionResult<TaskDto>> Assign(int id, AssignTaskRequest request, CancellationToken cancellationToken) =>
        Change(
            id,
            request.Version,
            async task =>
            {
                if (request.AssigneeId is null)
                {
                    task.Unassign();
                    return;
                }

                var assignee = await db.Employees.SingleOrDefaultAsync(e => e.Id == request.AssigneeId, cancellationToken)
                    ?? throw new DomainException($"Employee {request.AssigneeId} does not exist.");
                task.AssignTo(assignee);
            },
            cancellationToken);

    [HttpPut("{id:int}/status")]
    public Task<ActionResult<TaskDto>> Move(int id, MoveTaskRequest request, CancellationToken cancellationToken) =>
        Change(
            id,
            request.Version,
            task =>
            {
                task.MoveTo(request.Status, Today);
                return Task.CompletedTask;
            },
            cancellationToken);

    private async Task<ActionResult<TaskDto>> Change(
        int id,
        string version,
        Func<ProjectTask, Task> change,
        CancellationToken cancellationToken)
    {
        var task = await Load(id, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Id == task.ProjectId)
            .Select(p => new { p.Number, p.Status })
            .SingleAsync(cancellationToken);
        if (project.Status != ProjectStatus.Active)
        {
            throw new DomainException($"Project {project.Number} is {project.Status}; its tasks cannot change.");
        }

        db.Entry(task).ExpectVersion(version);
        await change(task);
        await db.SaveChangesAsync(cancellationToken);
        return task.ToDto(db, project.Number);
    }

    private Task<ProjectTask?> Load(int id, CancellationToken cancellationToken) =>
        db.ProjectTasks.Include(t => t.Assignee).SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    private Task<string> ProjectNumber(ProjectTask task, CancellationToken cancellationToken) =>
        db.Projects.Where(p => p.Id == task.ProjectId).Select(p => p.Number).SingleAsync(cancellationToken);

    private IQueryable<TaskDto> Query(
        int? projectId,
        int? assigneeId,
        ProjectTaskStatus? status,
        bool activeProjectsOnly,
        bool openOnly)
    {
        var query =
            from task in db.ProjectTasks.AsNoTracking()
            join project in db.Projects on task.ProjectId equals project.Id
            where (projectId == null || task.ProjectId == projectId)
                && (assigneeId == null || task.AssigneeId == assigneeId)
                && (status == null || task.Status == status)
                && (!activeProjectsOnly || project.Status == ProjectStatus.Active)
                && (!openOnly || task.Status != ProjectTaskStatus.Done)
            orderby project.Number, task.DueDate
            select new TaskDto(
                task.Id,
                task.ProjectId,
                project.Number,
                task.Title,
                task.Description,
                task.Discipline,
                task.Stage,
                task.Status,
                task.Priority,
                task.AssigneeId,
                task.Assignee == null ? null : task.Assignee.FullName,
                task.DueDate,
                task.EstimatedHours,
                task.CompletedOn,
                Convert.ToBase64String(EF.Property<byte[]>(task, Tracking.RowVersion)));
        return query;
    }
}
