using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using VoltDesk.Api.Auth;
using VoltDesk.Contracts;
using VoltDesk.Core.Employees;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    VoltDeskDbContext db,
    IPasswordHasher<Employee> hasher,
    TokenService tokens) : ControllerBase
{
    // Verified when the email is unknown, so response time does not reveal which emails exist.
    private static readonly Employee TimingDummy = Employee.Create("Timing Dummy", "dummy@voltdesk.invalid", EmployeeRole.Engineer);
    private static readonly Lazy<string> TimingDummyHash = new(() => new PasswordHasher<Employee>().HashPassword(TimingDummy, "not-a-real-password"));

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.LoginRateLimit)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Email == email, cancellationToken);

        if (employee?.PasswordHash is null)
        {
            hasher.VerifyHashedPassword(TimingDummy, TimingDummyHash.Value, request.Password);
            return SignInFailed();
        }

        var result = hasher.VerifyHashedPassword(employee, employee.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed || !employee.IsActive)
        {
            return SignInFailed();
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            employee.SetPasswordHash(hasher.HashPassword(employee, request.Password));
            await db.SaveChangesAsync(cancellationToken);
        }

        var (token, expiresAt) = tokens.Issue(employee);
        return new LoginResponse(token, expiresAt, employee.ToDto(db));
    }

    [HttpGet("me")]
    public async Task<ActionResult<EmployeeDto>> Me(CancellationToken cancellationToken)
    {
        var id = User.EmployeeId();
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        return employee is null ? NotFound() : employee.ToDto(db);
    }

    private ObjectResult SignInFailed() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Sign-in failed",
        detail: "Wrong email or password, or the account is disabled.");
}
