using System.Net.Http;
using System.Net.Http.Headers;
using CommunityToolkit.Mvvm.ComponentModel;
using VoltDesk.Contracts;
using VoltDesk.Core.Employees;

namespace VoltDesk.Desktop.Services;

/// <summary>The signed-in employee and their access token. Lives in memory only.</summary>
public sealed partial class Session : ObservableObject
{
    public string? AccessToken { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn), nameof(CanManageProjects), nameof(RoleLabel))]
    public partial EmployeeDto? User { get; private set; }

    public bool IsSignedIn => User is not null;

    public bool CanManageProjects => User?.Role is EmployeeRole.ProjectManager or EmployeeRole.Admin;

    public string RoleLabel => User?.Role switch
    {
        EmployeeRole.ProjectManager => "Project manager",
        EmployeeRole.Admin => "Admin",
        EmployeeRole.Engineer => "Engineer",
        _ => string.Empty,
    };

    public void Start(LoginResponse login)
    {
        AccessToken = login.AccessToken;
        User = login.Employee;
    }

    public void End()
    {
        AccessToken = null;
        User = null;
    }
}

/// <summary>Adds the bearer token of the current session to every API request.</summary>
public sealed class BearerTokenHandler(Session session) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (session.AccessToken is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>Sent when the API rejects the token; the shell returns to the sign-in screen.</summary>
public sealed record SessionExpiredMessage(string Reason);
