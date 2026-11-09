using System.Net;
using VoltDesk.Contracts;
using VoltDesk.Core.Projects;

namespace VoltDesk.Api.Tests;

/// <summary>Two people open the same record; the one who saves second must not overwrite silently.</summary>
public sealed class ConcurrencyTests(ApiFactory api)
{
    [Fact]
    public async Task Second_edit_of_a_project_with_a_stale_version_is_rejected()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync();

        var first = await manager.PutJsonAsync($"/api/projects/{project.Id}", new UpdateProjectRequest(
            "Renamed by Dana", project.BuildingType, project.City, project.State, project.Fee, project.Version));
        var second = await manager.PutJsonAsync($"/api/projects/{project.Id}", new UpdateProjectRequest(
            "Renamed by Marcus", project.BuildingType, project.City, project.State, 99m, project.Version));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var problem = await second.ReadProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal("Changed by someone else", problem.Title);
        var stored = await manager.GetAsync<ProjectDto>($"/api/projects/{project.Id}");
        Assert.Equal("Renamed by Dana", stored.Name);
    }

    [Fact]
    public async Task Stale_milestone_edit_is_rejected_although_only_the_milestone_row_changes()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync();
        var dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

        await (await manager.PutJsonAsync($"/api/projects/{project.Id}/milestones/{DesignStage.SchematicDesign}",
            new ScheduleMilestoneRequest(dueDate, project.Version))).ReadAsync<ProjectDto>();
        var stale = await manager.PutJsonAsync($"/api/projects/{project.Id}/milestones/{DesignStage.DesignDevelopment}",
            new ScheduleMilestoneRequest(dueDate.AddDays(30), project.Version));

        await stale.ReadProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Stale_contact_edit_is_rejected_at_client_level()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var client = await (await manager.PostJsonAsync("/api/clients", new CreateClientRequest($"Client {Guid.NewGuid():N}")))
            .ReadAsync<ClientDto>();

        var updated = await (await manager.PostJsonAsync($"/api/clients/{client.Id}/contacts",
            new ContactRequest("Mia Chen", "Facilities Director", "mia@client.example", null, false, client.Version))).ReadAsync<ClientDto>();
        var stale = await manager.PostJsonAsync($"/api/clients/{client.Id}/contacts",
            new ContactRequest("Leo Park", null, "leo@client.example", null, false, client.Version));

        Assert.True(updated.Contacts.Single().IsPrimary);
        Assert.NotEqual(client.Version, updated.Version);
        await stale.ReadProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Two_people_moving_the_same_card_conflict()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var me = await manager.GetAsync<EmployeeDto>("/api/auth/me");
        var project = await manager.CreateProjectAsync();
        var task = await (await manager.PostJsonAsync($"/api/projects/{project.Id}/tasks",
            new CreateTaskRequest("Fire alarm riser", null, Discipline.FireAlarm, TaskPriority.Normal, null, null))).ReadAsync<TaskDto>();
        task = await (await manager.PutJsonAsync($"/api/tasks/{task.Id}/assignee", new AssignTaskRequest(me.Id, task.Version)))
            .ReadAsync<TaskDto>();

        var first = await manager.PutJsonAsync($"/api/tasks/{task.Id}/status", new MoveTaskRequest(ProjectTaskStatus.InProgress, task.Version));
        var second = await manager.PutJsonAsync($"/api/tasks/{task.Id}/status", new MoveTaskRequest(ProjectTaskStatus.Done, task.Version));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await second.ReadProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Malformed_version_is_a_bad_request()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync();

        var response = await manager.PostJsonAsync($"/api/projects/{project.Id}/hold", new VersionRequest("not-a-version"));

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
    }
}
