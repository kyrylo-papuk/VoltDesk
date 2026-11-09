using System.Globalization;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using VoltDesk.Core.Employees;
using VoltDesk.Infrastructure;

namespace VoltDesk.Api.Auth;

public static class Claims
{
    public const string Role = "role";

    public static int? EmployeeId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), CultureInfo.InvariantCulture, out var id) ? id : null;
}

public static class Policies
{
    public const string ManageProjects = nameof(ManageProjects);
    public const string Admin = nameof(Admin);

    public static readonly string[] ManagerRoles = [nameof(EmployeeRole.ProjectManager), nameof(EmployeeRole.Admin)];
}

/// <summary>The signed-in employee, recorded as the author of audited changes.</summary>
internal sealed class HttpContextActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public int? EmployeeId => accessor.HttpContext?.User.EmployeeId();
}
