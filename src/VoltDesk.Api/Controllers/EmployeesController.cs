using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltDesk.Api.Auth;
using VoltDesk.Contracts;
using VoltDesk.Core.Common;
using VoltDesk.Core.Employees;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Api.Controllers;

[ApiController]
[Route("api/employees")]
public sealed class EmployeesController(VoltDeskDbContext db, IPasswordHasher<Employee> hasher) : ControllerBase
{
    [HttpGet]
    public async Task<List<EmployeeDto>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        var query = db.Employees.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        return await query
            .OrderBy(e => e.FullName)
            .Select(e => new EmployeeDto(
                e.Id, e.FullName, e.Email, e.Role, e.IsActive,
                Convert.ToBase64String(EF.Property<byte[]>(e, Tracking.RowVersion))))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Get(int id, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        return employee is null ? NotFound() : employee.ToDto(db);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = Employee.Create(request.FullName, request.Email, request.Role);
        employee.SetPasswordHash(hasher.HashPassword(employee, request.InitialPassword));
        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = employee.Id }, employee.ToDto(db));
    }

    [HttpPut("{id:int}/role")]
    [Authorize(Policy = Policies.Admin)]
    public Task<ActionResult<EmployeeDto>> ChangeRole(int id, ChangeRoleRequest request, CancellationToken cancellationToken) =>
        Change(id, request.Version, e => e.ChangeRole(request.Role), cancellationToken);

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = Policies.Admin)]
    public Task<ActionResult<EmployeeDto>> Deactivate(int id, VersionRequest request, CancellationToken cancellationToken) =>
        Change(id, request.Version, e => e.Deactivate(), cancellationToken);

    [HttpPost("{id:int}/reactivate")]
    [Authorize(Policy = Policies.Admin)]
    public Task<ActionResult<EmployeeDto>> Reactivate(int id, VersionRequest request, CancellationToken cancellationToken) =>
        Change(id, request.Version, e => e.Reactivate(), cancellationToken);

    private async Task<ActionResult<EmployeeDto>> Change(
        int id,
        string version,
        Action<Employee> change,
        CancellationToken cancellationToken)
    {
        if (id == User.EmployeeId())
        {
            // Prevents an admin from locking the studio out of administration by accident.
            throw new DomainException("You cannot change your own role or deactivate yourself.");
        }

        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employee is null)
        {
            return NotFound();
        }

        db.Entry(employee).ExpectVersion(version);
        change(employee);
        await db.SaveChangesAsync(cancellationToken);
        return employee.ToDto(db);
    }
}
