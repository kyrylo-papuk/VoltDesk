using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using VoltDesk.Contracts;
using VoltDesk.Core.Projects;

namespace VoltDesk.Api.Tests;

internal static class HttpExtensions
{
    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, ApiFactory.Json, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, ApiFactory.Json, TestContext.Current.CancellationToken);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json, TestContext.Current.CancellationToken))!;
    }

    public static async Task<ProblemDetails> ReadProblemAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json, TestContext.Current.CancellationToken))!;
    }

    public static async Task<T> GetAsync<T>(this HttpClient client, string url) =>
        await (await client.GetAsync(url, TestContext.Current.CancellationToken)).ReadAsync<T>();

    /// <summary>Creates a client and a project with a unique number, as the signed-in manager.</summary>
    public static async Task<ProjectDto> CreateProjectAsync(this HttpClient manager, string? name = null)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var client = await (await manager.PostJsonAsync("/api/clients", new CreateClientRequest($"Client {unique}"))).ReadAsync<ClientDto>();
        var me = await manager.GetAsync<EmployeeDto>("/api/auth/me");

        var request = new CreateProjectRequest(
            $"T-{unique}", name ?? $"Project {unique}", client.Id, BuildingType.Healthcare, "Denver", "CO", me.Id, 50_000m);
        return await (await manager.PostJsonAsync("/api/projects", request)).ReadAsync<ProjectDto>();
    }
}
