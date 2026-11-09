using System.Net;
using VoltDesk.Contracts;
using VoltDesk.Core.Employees;

namespace VoltDesk.Api.Tests;

public sealed class AuthTests(ApiFactory api)
{
    [Fact]
    public async Task Demo_manager_signs_in_and_reads_own_profile()
    {
        var client = await api.SignInAsync(ApiFactory.Manager);

        var me = await client.GetAsync<EmployeeDto>("/api/auth/me");

        Assert.Equal("Dana Whitfield", me.FullName);
        Assert.Equal(EmployeeRole.ProjectManager, me.Role);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var response = await api.CreateClient().PostJsonAsync(
            "/api/auth/login", new LoginRequest(ApiFactory.Manager, "wrong-password"));

        var problem = await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("Sign-in failed", problem.Title);
    }

    [Fact]
    public async Task Requests_without_a_token_are_rejected()
    {
        var response = await api.CreateClient().GetAsync("/api/projects", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_check_is_public()
    {
        var response = await api.CreateClient().GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Role_change_invalidates_tokens_issued_before_it()
    {
        var admin = await api.SignInAsync(ApiFactory.Admin);
        var (employee, password) = await CreateEmployeeAsync(admin, EmployeeRole.ProjectManager);
        var manager = await api.SignInAsync(employee.Email, password);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/projects", TestContext.Current.CancellationToken)).StatusCode);

        var demoted = await (await admin.PutJsonAsync(
            $"/api/employees/{employee.Id}/role", new ChangeRoleRequest(EmployeeRole.Engineer, employee.Version))).ReadAsync<EmployeeDto>();

        Assert.Equal(EmployeeRole.Engineer, demoted.Role);
        Assert.Equal(HttpStatusCode.Unauthorized, (await manager.GetAsync("/api/projects", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Deactivated_employee_loses_access_immediately()
    {
        var admin = await api.SignInAsync(ApiFactory.Admin);
        var (employee, password) = await CreateEmployeeAsync(admin, EmployeeRole.Engineer);
        var engineer = await api.SignInAsync(employee.Email, password);

        await (await admin.PostJsonAsync($"/api/employees/{employee.Id}/deactivate", new VersionRequest(employee.Version)))
            .ReadAsync<EmployeeDto>();

        Assert.Equal(HttpStatusCode.Unauthorized, (await engineer.GetAsync("/api/tasks/mine", TestContext.Current.CancellationToken)).StatusCode);
        var login = await api.CreateClient().PostJsonAsync("/api/auth/login", new LoginRequest(employee.Email, password));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    internal static async Task<(EmployeeDto Employee, string Password)> CreateEmployeeAsync(HttpClient admin, EmployeeRole role)
    {
        const string password = "Temporary-password-1";
        var email = $"user.{Guid.NewGuid():N}@voltdesk.example";
        var employee = await (await admin.PostJsonAsync(
            "/api/employees", new CreateEmployeeRequest("Test User", email, role, password))).ReadAsync<EmployeeDto>();
        return (employee, password);
    }
}
