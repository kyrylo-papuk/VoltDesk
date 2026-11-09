using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace VoltDesk.Desktop.Services;

/// <summary>An API call failed. <see cref="Exception.Message"/> is ready to show to the user.</summary>
public sealed class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public bool IsConflict => StatusCode == 409;

    public bool IsUnauthorized => StatusCode == 401;

    public static ApiException Unreachable() =>
        new(0, "The VoltDesk server is unreachable. Check your connection and try again.");

    public static async Task<ApiException> FromResponseAsync(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        ProblemResponse? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(ApiClient.Json);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Not a problem document; fall back to the status code.
        }

        var validation = problem?.Errors?.SelectMany(e => e.Value).ToList();
        var message = validation is { Count: > 0 }
            ? string.Join(Environment.NewLine, validation)
            : problem?.Detail ?? problem?.Title ?? status switch
            {
                401 => "Your session has ended. Sign in again.",
                403 => "Your role does not allow this action.",
                404 => "The record no longer exists.",
                _ => $"The server returned an error ({status}).",
            };
        return new ApiException(status, message);
    }

    private sealed record ProblemResponse(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
