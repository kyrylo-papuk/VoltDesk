using System.Net;
using VoltDesk.Contracts;
using VoltDesk.Core.Employees;

namespace VoltDesk.Api.Tests;

public sealed class AuthorizationTests(ApiFactory api)
{
    [Fact]
    public async Task Engineer_can_read_projects_but_not_create_clients()
    {
        var engineer = await api.SignInAsync(ApiFactory.Engineer);

        Assert.Equal(HttpStatusCode.OK, (await engineer.GetAsync("/api/projects", TestContext.Current.CancellationToken)).StatusCode);
        var response = await engineer.PostJsonAsync("/api/clients", new CreateClientRequest("Not Allowed Inc"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Project_manager_cannot_manage_employees()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);

        var response = await manager.PostJsonAsync(
            "/api/employees", new CreateEmployeeRequest("New Hire", "new.hire@voltdesk.example", EmployeeRole.Engineer, "Temporary-password-1"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_deactivate_themselves()
    {
        var admin = await api.SignInAsync(ApiFactory.Admin);
        var me = await admin.GetAsync<EmployeeDto>("/api/auth/me");

        var response = await admin.PostJsonAsync($"/api/employees/{me.Id}/deactivate", new VersionRequest(me.Version));

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Contains("yourself", problem.Detail);
    }
}
