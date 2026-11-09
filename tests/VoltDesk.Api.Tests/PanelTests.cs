using System.Net;
using ClosedXML.Excel;
using VoltDesk.Contracts;
using VoltDesk.Core.Panels;

namespace VoltDesk.Api.Tests;

public sealed class PanelTests(ApiFactory api)
{
    [Fact]
    public async Task Engineer_builds_a_panel_schedule_and_gets_the_calculation_back()
    {
        var (engineer, panel) = await NewPanelAsync();

        panel = await AddCircuitAsync(engineer, panel, 1, "Lighting", 1200m);
        panel = await AddCircuitAsync(engineer, panel, 2, "Water heater", 4160m, poles: 2, breaker: 30);

        Assert.Equal(["1", "2,4"], panel.Circuits.Select(c => c.Label));
        var phaseA = panel.Calculation.Phases.Single(p => p.Phase == Phase.A);
        Assert.Equal(1200m + 2080m, phaseA.Va);
        Assert.Equal(27.3m, phaseA.Amps);
        Assert.Equal(20m, panel.Circuits.Single(c => c.Poles == 2).Amps);
    }

    [Fact]
    public async Task Overlapping_slot_is_a_business_rule_error()
    {
        var (engineer, panel) = await NewPanelAsync();
        panel = await AddCircuitAsync(engineer, panel, 1, "RTU-1", 9000m, poles: 3, breaker: 40);

        var response = await engineer.PostJsonAsync($"/api/panels/{panel.Id}/circuits",
            new CircuitRequest(3, "Receptacles", LoadType.Receptacles, 900m, 1, 20, panel.Version));

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Contains("Slot 3 is taken by circuit 1,3,5", problem.Detail);
    }

    [Fact]
    public async Task Balancing_reduces_the_phase_imbalance()
    {
        var (engineer, panel) = await NewPanelAsync();
        foreach (var slot in new[] { 1, 7, 13, 19 })
        {
            panel = await AddCircuitAsync(engineer, panel, slot, $"Heater {slot}", 1500m);
        }

        var result = await (await engineer.PostJsonAsync($"/api/panels/{panel.Id}/balance", new VersionRequest(panel.Version)))
            .ReadAsync<BalanceResultDto>();

        // Four equal loads on three phases: the best split is 2-1-1, which is 50% by the NEMA definition.
        Assert.Equal(200m, result.ImbalanceBefore);
        Assert.Equal(50m, result.ImbalanceAfter);
        Assert.True(result.MovedCircuits > 0);
        Assert.Equal(result.ImbalanceAfter, result.Panel.Calculation.ImbalancePercent);
    }

    [Fact]
    public async Task Stale_panel_version_is_rejected()
    {
        var (engineer, panel) = await NewPanelAsync();
        await AddCircuitAsync(engineer, panel, 1, "Lighting", 500m);

        var stale = await engineer.PostJsonAsync($"/api/panels/{panel.Id}/circuits",
            new CircuitRequest(2, "Receptacles", LoadType.Receptacles, 500m, 1, 20, panel.Version));

        await stale.ReadProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Panels_of_a_project_on_hold_are_read_only()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync();
        var panel = await CreatePanelAsync(manager, project.Id);
        await (await manager.PostJsonAsync($"/api/projects/{project.Id}/hold", new VersionRequest(project.Version))).ReadAsync<ProjectDto>();

        var response = await manager.PostJsonAsync($"/api/panels/{panel.Id}/circuits",
            new CircuitRequest(1, "Lighting", LoadType.Lighting, 500m, 1, 20, panel.Version));

        var problem = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Contains("OnHold", problem.Detail);
    }

    [Fact]
    public async Task Export_is_a_workbook_with_live_phase_totals()
    {
        var (engineer, panel) = await NewPanelAsync();
        panel = await AddCircuitAsync(engineer, panel, 1, "Lighting", 1200m);
        panel = await AddCircuitAsync(engineer, panel, 3, "Receptacles", 900m);

        var response = await engineer.GetAsync($"/api/panels/{panel.Id}/export", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(panel.Name);
        Assert.Equal("Lighting", sheet.Cell(7, 2).GetString());
        Assert.Equal("SUM(H7:H8)", sheet.Cell(9, 8).FormulaA1);
        Assert.Equal("SUM(I7:I8)", sheet.Cell(9, 9).FormulaA1);
    }

    [Fact]
    public async Task Demo_panel_flags_the_undersized_sterilizer_breaker()
    {
        var engineer = await api.SignInAsync(ApiFactory.Engineer);
        var project = (await engineer.GetAsync<List<ProjectSummaryDto>>("/api/projects?search=2026-014")).Single();
        var summary = (await engineer.GetAsync<List<PanelSummaryDto>>($"/api/projects/{project.Id}/panels")).Single(p => p.Name == "LP-2A");

        var panel = await engineer.GetAsync<PanelDto>($"/api/panels/{summary.Id}");

        Assert.Contains(panel.Calculation.Warnings, w => w.StartsWith("Circuit 19:", StringComparison.Ordinal));
        Assert.True(panel.Calculation.ImbalancePercent > 50m);
    }

    private async Task<(HttpClient Engineer, PanelDto Panel)> NewPanelAsync()
    {
        var manager = await api.SignInAsync(ApiFactory.Manager);
        var project = await manager.CreateProjectAsync();
        var engineer = await api.SignInAsync(ApiFactory.Engineer);
        return (engineer, await CreatePanelAsync(engineer, project.Id));
    }

    private static async Task<PanelDto> CreatePanelAsync(HttpClient client, int projectId) =>
        await (await client.PostJsonAsync($"/api/projects/{projectId}/panels", new CreatePanelRequest(
            "LP-1", "Level 1", VoltageSystem.ThreePhase208Y120, BusRatingAmps: 225, MainBreakerAmps: 200, Spaces: 42))).ReadAsync<PanelDto>();

    private static async Task<PanelDto> AddCircuitAsync(
        HttpClient client, PanelDto panel, int slot, string description, decimal loadVa, int poles = 1, int breaker = 20) =>
        await (await client.PostJsonAsync($"/api/panels/{panel.Id}/circuits",
            new CircuitRequest(slot, description, LoadType.Equipment, loadVa, poles, breaker, panel.Version))).ReadAsync<PanelDto>();
}
