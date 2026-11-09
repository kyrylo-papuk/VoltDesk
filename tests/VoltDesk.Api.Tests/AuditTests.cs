using Microsoft.EntityFrameworkCore;
using VoltDesk.Contracts;
using VoltDesk.Core.Employees;
using VoltDesk.Core.Projects;

namespace VoltDesk.Api.Tests;

public sealed class AuditTests(ApiFactory api)
{
    [Fact]
    public async Task Project_history_shows_who_changed_what()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync(name: "Original name");

        await (await manager.PutJsonAsync($"/api/projects/{project.Id}", new UpdateProjectRequest(
            "New name", project.BuildingType, project.City, project.State, 75_000m, project.Version))).ReadAsync<ProjectDto>();

        var history = await manager.GetAsync<List<AuditEntryDto>>($"/api/projects/{project.Id}/history");
        var change = history[0];
        Assert.Equal("Modified", change.Action);
        Assert.Equal("Dana Whitfield", change.ActorName);
        Assert.Equal(new AuditValueChange("Original name", "New name"), change.Changes["Name"]);
        Assert.Equal(new AuditValueChange("50000.00", "75000"), change.Changes["Fee"]);
        Assert.Contains(history, h => h.Action == "Added" && h.EntityType == "Project");
    }

    [Fact]
    public async Task Task_and_milestone_changes_belong_to_the_project_history()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync();
        var dueDate = new DateOnly(2027, 3, 1);
        project = await (await manager.PutJsonAsync($"/api/projects/{project.Id}/milestones/{DesignStage.SchematicDesign}",
            new ScheduleMilestoneRequest(dueDate, project.Version))).ReadAsync<ProjectDto>();
        await (await manager.PostJsonAsync($"/api/projects/{project.Id}/tasks",
            new CreateTaskRequest("Short-circuit study", null, Discipline.Power, TaskPriority.High, null, 24m))).ReadAsync<TaskDto>();

        var history = await manager.GetAsync<List<AuditEntryDto>>($"/api/projects/{project.Id}/history");

        Assert.Contains(history, h => h.EntityType == "ProjectTask" && h.Action == "Added"
            && h.Changes["Title"].New == "Short-circuit study");
        Assert.Contains(history, h => h.Changes.TryGetValue("SD.DueDate", out var change) && change.New == "2027-03-01");
    }

    [Fact]
    public async Task Password_hashes_never_reach_the_audit_log()
    {
        var admin = await api.SignInAsync(ApiFactory.Admin);
        var (employee, _) = await AuthTests.CreateEmployeeAsync(admin, EmployeeRole.Engineer);

        var stored = await api.WithDbAsync(db => db.AuditEntries
            .SingleAsync(a => a.EntityType == nameof(Employee) && a.EntityId == employee.Id && a.Action == "Added", TestContext.Current.CancellationToken));

        Assert.Contains("\"PasswordHash\":{\"Old\":null,\"New\":\"***\"}", stored.Changes);
        Assert.NotNull(stored.ActorId);
    }
}
