using System.Net;
using System.Net.Http;

namespace VoltDesk.Desktop.Services;

public enum ApiHealth
{
    Healthy,
    DatabaseUnavailable,
    Unreachable,
}

public sealed class ApiClient(HttpClient http)
{
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
}
