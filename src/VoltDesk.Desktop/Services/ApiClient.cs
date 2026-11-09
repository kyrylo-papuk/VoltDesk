using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VoltDesk.Contracts;
using VoltDesk.Core.Projects;

namespace VoltDesk.Desktop.Services;

public enum ApiHealth
{
    Healthy,
    DatabaseUnavailable,
    Unreachable,
}

/// <summary>Typed client for the VoltDesk API. Failures surface as <see cref="ApiException"/>.</summary>
public sealed class ApiClient(HttpClient http)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<ApiHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync("health", cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => ApiHealth.Healthy,
                HttpStatusCode.ServiceUnavailable => ApiHealth.DatabaseUnavailable,
                _ => ApiHealth.Unreachable,
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ApiHealth.Unreachable;
        }
    }

    public Task<LoginResponse> LoginAsync(string email, string password) =>
        SendAsync<LoginResponse>(HttpMethod.Post, "api/auth/login", new LoginRequest(email, password));

    // Projects
    public Task<List<ProjectSummaryDto>> GetProjectsAsync(string? search, ProjectStatus? status) =>
        SendAsync<List<ProjectSummaryDto>>(HttpMethod.Get, WithQuery("api/projects", ("search", search), ("status", status?.ToString())));

    public Task<ProjectDto> GetProjectAsync(int id) => SendAsync<ProjectDto>(HttpMethod.Get, $"api/projects/{id}");

    public Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request) =>
        SendAsync<ProjectDto>(HttpMethod.Post, "api/projects", request);

    /// <param name="action">advance, hold, resume or cancel.</param>
    public Task<ProjectDto> ChangeProjectStateAsync(int id, string action, string version) =>
        SendAsync<ProjectDto>(HttpMethod.Post, $"api/projects/{id}/{action}", new VersionRequest(version));

    public Task<ProjectDto> ScheduleMilestoneAsync(int id, DesignStage stage, DateOnly dueDate, string version) =>
        SendAsync<ProjectDto>(HttpMethod.Put, $"api/projects/{id}/milestones/{stage}", new ScheduleMilestoneRequest(dueDate, version));

    public Task<List<AuditEntryDto>> GetProjectHistoryAsync(int id) =>
        SendAsync<List<AuditEntryDto>>(HttpMethod.Get, $"api/projects/{id}/history");

    // Clients
    public Task<List<ClientSummaryDto>> GetClientsAsync(string? search) =>
        SendAsync<List<ClientSummaryDto>>(HttpMethod.Get, WithQuery("api/clients", ("search", search)));

    public Task<ClientDto> GetClientAsync(int id) => SendAsync<ClientDto>(HttpMethod.Get, $"api/clients/{id}");

    public Task<ClientDto> CreateClientAsync(string name) =>
        SendAsync<ClientDto>(HttpMethod.Post, "api/clients", new CreateClientRequest(name));

    public Task<ClientDto> AddContactAsync(int clientId, ContactRequest request) =>
        SendAsync<ClientDto>(HttpMethod.Post, $"api/clients/{clientId}/contacts", request);

    public Task<ClientDto> RemoveContactAsync(int clientId, int contactId, string version) =>
        SendAsync<ClientDto>(HttpMethod.Delete, WithQuery($"api/clients/{clientId}/contacts/{contactId}", ("version", version)));

    // Tasks
    public Task<List<TaskDto>> GetBoardTasksAsync(int? projectId, int? assigneeId) =>
        SendAsync<List<TaskDto>>(HttpMethod.Get, WithQuery(
            "api/tasks",
            ("activeProjectsOnly", "true"),
            ("projectId", projectId?.ToString()),
            ("assigneeId", assigneeId?.ToString())));

    public Task<TaskDto> CreateTaskAsync(int projectId, CreateTaskRequest request) =>
        SendAsync<TaskDto>(HttpMethod.Post, $"api/projects/{projectId}/tasks", request);

    public Task<TaskDto> UpdateTaskAsync(int id, UpdateTaskRequest request) =>
        SendAsync<TaskDto>(HttpMethod.Put, $"api/tasks/{id}", request);

    public Task<TaskDto> AssignTaskAsync(int id, int? assigneeId, string version) =>
        SendAsync<TaskDto>(HttpMethod.Put, $"api/tasks/{id}/assignee", new AssignTaskRequest(assigneeId, version));

    public Task<TaskDto> MoveTaskAsync(int id, ProjectTaskStatus status, string version) =>
        SendAsync<TaskDto>(HttpMethod.Put, $"api/tasks/{id}/status", new MoveTaskRequest(status, version));

    // Panel schedules
    public Task<List<PanelSummaryDto>> GetPanelsAsync(int projectId) =>
        SendAsync<List<PanelSummaryDto>>(HttpMethod.Get, $"api/projects/{projectId}/panels");

    public Task<PanelDto> CreatePanelAsync(int projectId, CreatePanelRequest request) =>
        SendAsync<PanelDto>(HttpMethod.Post, $"api/projects/{projectId}/panels", request);

    public Task<PanelDto> GetPanelAsync(int id) => SendAsync<PanelDto>(HttpMethod.Get, $"api/panels/{id}");

    public Task<PanelDto> AddCircuitAsync(int panelId, CircuitRequest request) =>
        SendAsync<PanelDto>(HttpMethod.Post, $"api/panels/{panelId}/circuits", request);

    public Task<PanelDto> UpdateCircuitAsync(int panelId, int circuitId, CircuitRequest request) =>
        SendAsync<PanelDto>(HttpMethod.Put, $"api/panels/{panelId}/circuits/{circuitId}", request);

    public Task<PanelDto> RemoveCircuitAsync(int panelId, int circuitId, string version) =>
        SendAsync<PanelDto>(HttpMethod.Delete, WithQuery($"api/panels/{panelId}/circuits/{circuitId}", ("version", version)));

    public Task<BalanceResultDto> BalancePanelAsync(int panelId, string version) =>
        SendAsync<BalanceResultDto>(HttpMethod.Post, $"api/panels/{panelId}/balance", new VersionRequest(version));

    /// <summary>Downloads the panel schedule workbook with the file name the server suggests.</summary>
    public async Task<(byte[] Content, string FileName)> ExportPanelAsync(int panelId)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync($"api/panels/{panelId}/export");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw ApiException.Unreachable();
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await ApiException.FromResponseAsync(response);
            }

            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? "panel schedule.xlsx";
            return (await response.Content.ReadAsByteArrayAsync(), fileName);
        }
    }

    // Google Workspace
    public Task<GoogleStatusDto> GetGoogleStatusAsync() => SendAsync<GoogleStatusDto>(HttpMethod.Get, "api/integrations/google");

    public Task<ExternalFileDto> SavePanelToDriveAsync(int panelId) =>
        SendAsync<ExternalFileDto>(HttpMethod.Post, $"api/panels/{panelId}/export/google-drive");

    public Task<ExternalFileDto> ExportWorkloadToSheetsAsync() =>
        SendAsync<ExternalFileDto>(HttpMethod.Post, "api/reports/workload/google-sheets");

    // Employees
    public Task<List<EmployeeDto>> GetEmployeesAsync() => SendAsync<List<EmployeeDto>>(HttpMethod.Get, "api/employees");

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw ApiException.Unreachable();
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await ApiException.FromResponseAsync(response);
            }

            return (await response.Content.ReadFromJsonAsync<T>(Json))!;
        }
    }

    private static string WithQuery(string path, params (string Key, string? Value)[] parameters)
    {
        var query = string.Join('&', parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}"));
        return query.Length == 0 ? path : $"{path}?{query}";
    }
}
