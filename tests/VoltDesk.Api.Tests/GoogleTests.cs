using System.Net;
using Microsoft.EntityFrameworkCore;
using VoltDesk.Contracts;
using VoltDesk.Core.Panels;
using VoltDesk.Core.Projects;
using VoltDesk.Infrastructure.Integrations;

namespace VoltDesk.Api.Tests;

/// <summary>Google sync through the outbox, against an in-memory Google.</summary>
public sealed class GoogleTests(ApiFactory api)
{
    [Fact]
    public async Task New_project_gets_a_drive_folder_with_the_standard_subfolders()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync(name: "Clinic Fit-out");
        Assert.Null(project.DocumentsFolderUrl);

        await api.ProcessOutboxAsync();

        var synced = await manager.GetAsync<ProjectDto>($"/api/projects/{project.Id}");
        Assert.NotNull(synced.DocumentsFolderUrl);
        var folder = api.Google.Folders.Single(f => f.Value.Name == $"{project.Number} Clinic Fit-out");
        Assert.Equal("VoltDesk projects", api.Google.Folders[folder.Value.ParentId!].Name);
        var subfolders = api.Google.Folders.Values.Where(f => f.ParentId == folder.Key).Select(f => f.Name).ToList();
        Assert.Contains("Panel schedules", subfolders);
        Assert.Equal(8, subfolders.Count);
    }

    [Fact]
    public async Task Milestone_dates_become_one_calendar_event_that_is_updated_in_place()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync();
        var firstDate = new DateOnly(2027, 2, 1);

        project = await (await manager.PutJsonAsync($"/api/projects/{project.Id}/milestones/{DesignStage.SchematicDesign}",
            new ScheduleMilestoneRequest(firstDate, project.Version))).ReadAsync<ProjectDto>();
        await api.ProcessOutboxAsync();
        var eventId = Assert.Single(api.Google.Events, e => e.Value.ProjectNumber == project.Number).Key;

        project = await (await manager.PutJsonAsync($"/api/projects/{project.Id}/milestones/{DesignStage.SchematicDesign}",
            new ScheduleMilestoneRequest(firstDate.AddDays(7), project.Version))).ReadAsync<ProjectDto>();
        await (await manager.PostJsonAsync($"/api/projects/{project.Id}/advance", new VersionRequest(project.Version))).ReadAsync<ProjectDto>();
        await api.ProcessOutboxAsync();

        var calendarEvent = Assert.Single(api.Google.Events, e => e.Value.ProjectNumber == project.Number);
        Assert.Equal(eventId, calendarEvent.Key);
        Assert.Equal(firstDate.AddDays(7), calendarEvent.Value.DueDate);
        Assert.NotNull(calendarEvent.Value.CompletedOn);
        Assert.NotNull(calendarEvent.Value.FolderUrl);
    }

    [Fact]
    public async Task Google_outage_does_not_fail_the_request_and_is_retried_later()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var number = $"T-OUT{Guid.NewGuid():N}"[..14].ToUpperInvariant(); // project numbers are stored upper-case
        api.Google.FailingFolderPrefixes[number] = true;
        var client = await (await manager.PostJsonAsync("/api/clients", new CreateClientRequest($"Client {number}"))).ReadAsync<ClientDto>();
        var me = await manager.GetAsync<EmployeeDto>("/api/auth/me");

        var created = await manager.PostJsonAsync("/api/projects", new CreateProjectRequest(
            number, "Outage test", client.Id, BuildingType.Commercial, "Denver", "CO", me.Id, 0m));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await api.ProcessOutboxAsync();

        var message = await api.WithDbAsync(db => db.Outbox.AsNoTracking()
            .SingleAsync(m => m.Payload.Contains(number), TestContext.Current.CancellationToken));
        Assert.Null(message.ProcessedAt);
        Assert.Equal(1, message.Attempts);
        Assert.Contains("Simulated Google Drive outage", message.LastError);
        Assert.True(message.NextAttemptAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Panel_schedule_is_saved_to_the_panel_schedules_folder()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync();
        var panel = await (await manager.PostJsonAsync($"/api/projects/{project.Id}/panels", new CreatePanelRequest(
            "LP-3", null, VoltageSystem.ThreePhase208Y120, 225, 200, 42))).ReadAsync<PanelDto>();

        var file = await (await manager.PostJsonAsync<object?>($"/api/panels/{panel.Id}/export/google-drive", null)).ReadAsync<ExternalFileDto>();

        var upload = api.Google.Uploads.Single(u => u.Value.FileName == file.Name).Value;
        Assert.Equal("Panel schedules", api.Google.Folders[upload.FolderId].Name);
        Assert.StartsWith($"{project.Number} LP-3 panel schedule", file.Name);
        Assert.True(upload.Size > 0);
    }

    [Fact]
    public async Task Workload_report_is_written_to_a_google_sheet()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);

        var file = await (await manager.PostJsonAsync<object?>("/api/reports/workload/google-sheets", null)).ReadAsync<ExternalFileDto>();

        var sheet = api.Google.Spreadsheets.Single(s => s.Value.Title == file.Name).Value;
        Assert.Equal(["Employee", "Role", "Open tasks", "Overdue", "Estimated hours", "Projects"], sheet.Rows[0]);
        Assert.Contains(sheet.Rows, row => (string)row[0] == "Sam Ortega");
    }

    [Fact]
    public async Task Engineer_cannot_export_the_workload_report()
    {
        var engineer = await api.SignInAsync(ApiFactory.Engineer);

        var response = await engineer.PostJsonAsync<object?>("/api/reports/workload/google-sheets", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Status_reports_the_integration_as_enabled()
    {
        var engineer = await api.SignInAsync(ApiFactory.Engineer);

        var status = await engineer.GetAsync<GoogleStatusDto>("/api/integrations/google");

        Assert.True(status.Enabled);
    }

    [Fact]
    public async Task Outbox_and_links_are_not_written_to_the_audit_log()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        await manager.CreateProjectAsync();
        await api.ProcessOutboxAsync();

        var audited = await api.WithDbAsync(db => db.AuditEntries.AsNoTracking()
            .AnyAsync(a => a.EntityType == nameof(OutboxMessage) || a.EntityType == nameof(ExternalLink), TestContext.Current.CancellationToken));

        Assert.False(audited);
    }
}
