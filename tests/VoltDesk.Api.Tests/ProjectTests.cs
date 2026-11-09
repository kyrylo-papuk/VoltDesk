using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using VoltDesk.Contracts;
using VoltDesk.Core.Projects;

namespace VoltDesk.Api.Tests;

public sealed class ProjectTests(ApiFactory api)
{
    [Fact]
    public async Task Manager_creates_a_project_in_schematic_design_with_all_milestones()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);

        var project = await manager.CreateProjectAsync();

        Assert.Equal(DesignStage.SchematicDesign, project.Stage);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal(Enum.GetValues<DesignStage>(), project.Milestones.Select(m => m.Stage));
        var fetched = await manager.GetAsync<ProjectDto>($"/api/projects/{project.Id}");
        Assert.Equal(project.Version, fetched.Version);
        Assert.Equal(project.Milestones, fetched.Milestones);
    }

    [Fact]
    public async Task Stage_flow_from_task_to_next_stage()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var engineer = await api.SignInAsync(ApiFactory.Engineer);
        var engineerId = (await engineer.GetAsync<EmployeeDto>("/api/auth/me")).Id;
        var project = await manager.CreateProjectAsync();

        var task = await (await manager.PostJsonAsync($"/api/projects/{project.Id}/tasks",
            new CreateTaskRequest("Load calculation", "Main switchboard", Discipline.Power, TaskPriority.High, null, 8m))).ReadAsync<TaskDto>();
        var blocked = await manager.PostJsonAsync($"/api/projects/{project.Id}/advance", new VersionRequest(project.Version));
        var problem = await blocked.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Contains("1 open task", problem.Detail);

        task = await (await engineer.PutJsonAsync($"/api/tasks/{task.Id}/assignee", new AssignTaskRequest(engineerId, task.Version))).ReadAsync<TaskDto>();
        task = await (await engineer.PutJsonAsync($"/api/tasks/{task.Id}/status", new MoveTaskRequest(ProjectTaskStatus.Done, task.Version))).ReadAsync<TaskDto>();
        Assert.NotNull(task.CompletedOn);

        var advanced = await (await manager.PostJsonAsync($"/api/projects/{project.Id}/advance", new VersionRequest(project.Version)))
            .ReadAsync<ProjectDto>();
        Assert.Equal(DesignStage.DesignDevelopment, advanced.Stage);
        Assert.NotNull(advanced.Milestones[0].CompletedOn);
    }

    [Fact]
    public async Task Unassigned_task_cannot_start()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync();
        var task = await (await manager.PostJsonAsync($"/api/projects/{project.Id}/tasks",
            new CreateTaskRequest("Lighting layout", null, Discipline.Lighting, TaskPriority.Normal, null, null))).ReadAsync<TaskDto>();

        var response = await manager.PutJsonAsync($"/api/tasks/{task.Id}/status", new MoveTaskRequest(ProjectTaskStatus.InProgress, task.Version));

        await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Tasks_of_a_project_on_hold_cannot_change()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync();
        var task = await (await manager.PostJsonAsync($"/api/projects/{project.Id}/tasks",
            new CreateTaskRequest("Panel schedules", null, Discipline.Power, TaskPriority.Normal, null, null))).ReadAsync<TaskDto>();
        await (await manager.PostJsonAsync($"/api/projects/{project.Id}/hold", new VersionRequest(project.Version))).ReadAsync<ProjectDto>();

        var response = await manager.PutJsonAsync($"/api/tasks/{task.Id}",
            new UpdateTaskRequest("Renamed", null, Discipline.Power, TaskPriority.High, null, null, task.Version));

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Contains("OnHold", problem.Detail);
    }

    [Fact]
    public async Task Task_board_lists_only_tasks_of_active_projects()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var active = await manager.CreateProjectAsync();
        var paused = await manager.CreateProjectAsync();
        foreach (var project in new[] { active, paused })
        {
            await (await manager.PostJsonAsync($"/api/projects/{project.Id}/tasks",
                new CreateTaskRequest("Board task", null, Discipline.Power, TaskPriority.Normal, null, null))).ReadAsync<TaskDto>();
        }

        await (await manager.PostJsonAsync($"/api/projects/{paused.Id}/hold", new VersionRequest(paused.Version))).ReadAsync<ProjectDto>();

        var board = await manager.GetAsync<List<TaskDto>>("/api/tasks?activeProjectsOnly=true");
        Assert.Contains(board, t => t.ProjectId == active.Id);
        Assert.DoesNotContain(board, t => t.ProjectId == paused.Id);
        var all = await manager.GetAsync<List<TaskDto>>($"/api/tasks?projectId={paused.Id}");
        Assert.Single(all);
    }

    [Fact]
    public async Task Duplicate_project_number_returns_conflict_with_a_clear_message()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var existing = await manager.CreateProjectAsync();

        var response = await manager.PostJsonAsync("/api/projects", new CreateProjectRequest(
            existing.Number, "Copy", existing.ClientId, BuildingType.Commercial, "Denver", "CO", existing.ProjectManagerId, 0m));

        var problem = await response.ReadProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal("This project number is already used.", problem.Detail);
    }

    [Fact]
    public async Task Unknown_state_code_is_a_business_rule_error()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync();

        var response = await manager.PutJsonAsync($"/api/projects/{project.Id}", new UpdateProjectRequest(
            project.Name, project.BuildingType, project.City, "XX", project.Fee, project.Version));

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Contains("not a US state code", problem.Detail);
    }

    [Fact]
    public async Task Missing_required_fields_return_validation_errors()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);

        var response = await manager.PostJsonAsync("/api/projects", new CreateProjectRequest(
            "", "", 1, BuildingType.Commercial, "", "C", 1, -5m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(ApiFactory.Json, TestContext.Current.CancellationToken);
        Assert.Contains("Number", problem!.Errors.Keys);
        Assert.Contains("Name", problem.Errors.Keys);
        Assert.Contains("State", problem.Errors.Keys);
        Assert.Contains("Fee", problem.Errors.Keys);
    }
}
