using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltDesk.Contracts;
using VoltDesk.Core.Common;
using VoltDesk.Core.Panels;
using VoltDesk.Core.Projects;
using VoltDesk.Infrastructure.Export;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Api.Controllers;

/// <summary>Panel schedules. Engineers own this work, so every signed-in employee can edit.</summary>
[ApiController]
public sealed class PanelsController(VoltDeskDbContext db) : ControllerBase
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("api/projects/{projectId:int}/panels")]
    public async Task<List<PanelSummaryDto>> List(int projectId, CancellationToken cancellationToken)
    {
        var panels = await db.Panels.AsNoTracking()
            .Include(p => p.Circuits)
            .Where(p => p.ProjectId == projectId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return [.. panels.Select(p => p.ToSummaryDto())];
    }

    [HttpPost("api/projects/{projectId:int}/panels")]
    public async Task<ActionResult<PanelDto>> Create(int projectId, CreatePanelRequest request, CancellationToken cancellationToken)
    {
        var project = await ActiveProjectAsync(projectId, cancellationToken);
        var panel = ElectricalPanel.Create(
            projectId, request.Name, request.Location, request.System, request.BusRatingAmps, request.MainBreakerAmps, request.Spaces);
        db.Panels.Add(panel);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = panel.Id }, panel.ToDto(db, project.Number));
    }

    [HttpGet("api/panels/{id:int}")]
    public async Task<ActionResult<PanelDto>> Get(int id, CancellationToken cancellationToken)
    {
        var panel = await LoadAsync(id, cancellationToken);
        return panel is null ? NotFound() : panel.ToDto(db, await ProjectNumberAsync(panel, cancellationToken));
    }

    [HttpGet("api/panels/{id:int}/history")]
    public Task<List<AuditEntryDto>> History(int id, CancellationToken cancellationToken) =>
        db.HistoryAsync(nameof(ElectricalPanel), id, cancellationToken);

    [HttpPut("api/panels/{id:int}")]
    public Task<ActionResult<PanelDto>> Update(int id, UpdatePanelRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request.Version, p => p.UpdateDetails(request.Name, request.Location, request.BusRatingAmps, request.MainBreakerAmps), cancellationToken);

    [HttpPost("api/panels/{id:int}/circuits")]
    public Task<ActionResult<PanelDto>> AddCircuit(int id, CircuitRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(
            id,
            request.Version,
            p => p.AddCircuit(request.Slot, request.Description, request.LoadType, request.LoadVa, request.Poles, request.BreakerAmps),
            cancellationToken);

    /// <summary>Edits a circuit; a different <see cref="CircuitRequest.Slot"/> moves it.</summary>
    [HttpPut("api/panels/{id:int}/circuits/{circuitId:int}")]
    public Task<ActionResult<PanelDto>> UpdateCircuit(int id, int circuitId, CircuitRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(
            id,
            request.Version,
            p =>
            {
                var circuit = FindCircuit(p, circuitId);
                if (request.Poles != circuit.Poles)
                {
                    throw new DomainException("A breaker's poles cannot change; remove the circuit and add a new one.");
                }

                p.UpdateCircuit(circuit, request.Description, request.LoadType, request.LoadVa, request.BreakerAmps);
                if (request.Slot != circuit.Slot)
                {
                    p.MoveCircuit(circuit, request.Slot);
                }
            },
            cancellationToken);

    [HttpDelete("api/panels/{id:int}/circuits/{circuitId:int}")]
    public Task<ActionResult<PanelDto>> RemoveCircuit(
        int id,
        int circuitId,
        [FromQuery, Required] string version,
        CancellationToken cancellationToken) =>
        ChangeAsync(id, version, p => p.RemoveCircuit(FindCircuit(p, circuitId)), cancellationToken);

    [HttpPost("api/panels/{id:int}/balance")]
    public async Task<ActionResult<BalanceResultDto>> Balance(int id, VersionRequest request, CancellationToken cancellationToken)
    {
        BalanceResult? result = null;
        var changed = await ChangeAsync(id, request.Version, p => result = p.BalancePhases(), cancellationToken);
        if (changed.Value is not { } panel)
        {
            return changed.Result!;
        }

        return new BalanceResultDto(
            Math.Round(result!.ImbalanceBefore, 1), Math.Round(result.ImbalanceAfter, 1), result.MovedCircuits, panel);
    }

    [HttpGet("api/panels/{id:int}/export")]
    public async Task<IActionResult> Export(int id, CancellationToken cancellationToken)
    {
        var panel = await LoadAsync(id, cancellationToken);
        if (panel is null)
        {
            return NotFound();
        }

        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Id == panel.ProjectId)
            .Select(p => new { p.Number, p.Name })
            .SingleAsync(cancellationToken);
        var bytes = PanelScheduleWorkbook.Build(panel, project.Number, project.Name);
        return File(bytes, ExcelContentType, $"{project.Number} {panel.Name} panel schedule.xlsx");
    }

    private async Task<ActionResult<PanelDto>> ChangeAsync(
        int id,
        string version,
        Action<ElectricalPanel> change,
        CancellationToken cancellationToken)
    {
        var panel = await LoadAsync(id, cancellationToken);
        if (panel is null)
        {
            return NotFound();
        }

        var project = await ActiveProjectAsync(panel.ProjectId, cancellationToken);
        db.Entry(panel).ExpectVersion(version);
        change(panel);
        await db.SaveChangesAsync(cancellationToken);
        return panel.ToDto(db, project.Number);
    }

    private Task<ElectricalPanel?> LoadAsync(int id, CancellationToken cancellationToken) =>
        db.Panels.Include(p => p.Circuits).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    private Task<string> ProjectNumberAsync(ElectricalPanel panel, CancellationToken cancellationToken) =>
        db.Projects.Where(p => p.Id == panel.ProjectId).Select(p => p.Number).SingleAsync(cancellationToken);

    /// <summary>Panels of projects on hold or closed are read-only, like their tasks.</summary>
    private async Task<(string Number, ProjectStatus Status)> ActiveProjectAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Number, p.Status })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException($"Project {projectId} does not exist.");

        return project.Status == ProjectStatus.Active
            ? (project.Number, project.Status)
            : throw new DomainException($"Project {project.Number} is {project.Status}; its panels cannot change.");
    }

    private static Circuit FindCircuit(ElectricalPanel panel, int circuitId) =>
        panel.Circuits.SingleOrDefault(c => c.Id == circuitId)
        ?? throw new DomainException($"Panel {panel.Name} has no circuit {circuitId}.");
}
