using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltDesk.Api.Auth;
using VoltDesk.Contracts;
using VoltDesk.Core.Common;
using VoltDesk.Core.Employees;
using VoltDesk.Core.Projects;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(VoltDeskDbContext db, TimeProvider clock) : ControllerBase
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    [HttpGet]
    public async Task<List<ProjectSummaryDto>> List(
        [FromQuery] ProjectStatus? status,
        [FromQuery] DesignStage? stage,
        [FromQuery] int? clientId,
        [FromQuery] int? managerId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = db.Projects.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }

        if (stage is not null)
        {
            query = query.Where(p => p.Stage == stage);
        }

        if (clientId is not null)
        {
            query = query.Where(p => p.ClientId == clientId);
        }

        if (managerId is not null)
        {
            query = query.Where(p => p.ProjectManagerId == managerId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.Number.Contains(term) || p.Name.Contains(term) || p.Client.Name.Contains(term));
        }

        return await query
            .OrderByDescending(p => p.Number)
            .Select(p => new ProjectSummaryDto(
                p.Id,
                p.Number,
                p.Name,
                p.ClientId,
                p.Client.Name,
                p.BuildingType,
                p.City,
                p.State,
                p.Stage,
                p.Status,
                p.ProjectManagerId,
                p.ProjectManager.FullName,
                p.Fee,
                p.Tasks.Count(t => t.Status != ProjectTaskStatus.Done),
                p.Milestones.Where(m => m.Stage == p.Stage).Select(m => m.DueDate).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDto>> Get(int id, CancellationToken cancellationToken)
    {
        var project = await Load(id, cancellationToken);
        return project is null ? NotFound() : project.ToDto(db, Today);
    }

    [HttpGet("{id:int}/history")]
    public Task<List<AuditEntryDto>> History(int id, CancellationToken cancellationToken) =>
        db.HistoryAsync(nameof(Project), id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var client = await db.Clients.SingleOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken)
            ?? throw new DomainException($"Client {request.ClientId} does not exist.");
        var manager = await FindEmployee(request.ProjectManagerId, cancellationToken);

        var project = Project.Create(
            request.Number, request.Name, client, request.BuildingType, request.City, request.State, manager, request.Fee);
        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, project.ToDto(db, Today));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.ManageProjects)]
    public Task<ActionResult<ProjectDto>> Update(int id, UpdateProjectRequest request, CancellationToken cancellationToken) =>
        Change(
            id,
            request.Version,
            p => p.UpdateDetails(request.Name, request.BuildingType, request.City, request.State, request.Fee),
            cancellationToken);

    [HttpPut("{id:int}/manager")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<ActionResult<ProjectDto>> ChangeManager(
        int id,
        ChangeManagerRequest request,
        CancellationToken cancellationToken)
    {
        var manager = await FindEmployee(request.ProjectManagerId, cancellationToken);
        return await Change(id, request.Version, p => p.AssignManager(manager), cancellationToken);
    }

    [HttpPut("{id:int}/milestones/{stage}")]
    [Authorize(Policy = Policies.ManageProjects)]
    public Task<ActionResult<ProjectDto>> ScheduleMilestone(
        int id,
        DesignStage stage,
        ScheduleMilestoneRequest request,
        CancellationToken cancellationToken) =>
        Change(id, request.Version, p => p.ScheduleMilestone(stage, request.DueDate), cancellationToken);

    [HttpPost("{id:int}/advance")]
    [Authorize(Policy = Policies.ManageProjects)]
    public Task<ActionResult<ProjectDto>> Advance(int id, VersionRequest request, CancellationToken cancellationToken) =>
        Change(id, request.Version, p => p.AdvanceStage(Today), cancellationToken);

    [HttpPost("{id:int}/hold")]
    [Authorize(Policy = Policies.ManageProjects)]
    public Task<ActionResult<ProjectDto>> Hold(int id, VersionRequest request, CancellationToken cancellationToken) =>
        Change(id, request.Version, p => p.PutOnHold(), cancellationToken);

    [HttpPost("{id:int}/resume")]
    [Authorize(Policy = Policies.ManageProjects)]
    public Task<ActionResult<ProjectDto>> Resume(int id, VersionRequest request, CancellationToken cancellationToken) =>
        Change(id, request.Version, p => p.Resume(), cancellationToken);

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Policies.ManageProjects)]
    public Task<ActionResult<ProjectDto>> Cancel(int id, VersionRequest request, CancellationToken cancellationToken) =>
        Change(id, request.Version, p => p.Cancel(), cancellationToken);

    [HttpPost("{id:int}/tasks")]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<ActionResult<TaskDto>> AddTask(int id, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var project = await Load(id, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        var task = project.AddTask(
            request.Title, request.Discipline, request.Priority, request.DueDate, request.EstimatedHours, request.Description);

        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(TasksController.Get), "Tasks", new { id = task.Id }, task.ToDto(db, project.Number));
    }

    /// <summary>Loads the whole aggregate, applies a change against the version the client saw, saves.</summary>
    private async Task<ActionResult<ProjectDto>> Change(
        int id,
        string version,
        Action<Project> change,
        CancellationToken cancellationToken)
    {
        var project = await Load(id, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        db.Entry(project).ExpectVersion(version);
        change(project);
        await db.SaveChangesAsync(cancellationToken);
        return project.ToDto(db, Today);
    }

    // Domain rules such as "no open tasks when closing a stage" need the tasks loaded.
    private Task<Project?> Load(int id, CancellationToken cancellationToken) =>
        db.Projects
            .Include(p => p.Client)
            .Include(p => p.ProjectManager)
            .Include(p => p.Tasks).ThenInclude(t => t.Assignee)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    private async Task<Employee> FindEmployee(int id, CancellationToken cancellationToken) =>
        await db.Employees.SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
        ?? throw new DomainException($"Employee {id} does not exist.");
}
