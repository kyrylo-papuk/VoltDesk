using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VoltDesk.Core.Common;
using VoltDesk.Infrastructure.Integrations.Google;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Api.Errors;

/// <summary>Turns expected failures into RFC 7807 problem responses the desktop client can show.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    private static readonly Dictionary<string, string> UniqueIndexMessages = new()
    {
        ["IX_Projects_Number"] = "This project number is already used.",
        ["IX_Clients_Name"] = "A client with this name already exists.",
        ["IX_Employees_Email"] = "An employee with this email already exists.",
        ["IX_Contacts_ClientId_Email"] = "This client already has a contact with this email.",
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int Status, string Title, string Detail)? problem = exception switch
        {
            DomainException ex => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", ex.Message),
            InvalidVersionException ex => (StatusCodes.Status400BadRequest, "Invalid version", ex.Message),
            IntegrationDisabledException ex => (StatusCodes.Status503ServiceUnavailable, "Integration not configured", ex.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Changed by someone else",
                "This record was changed by another user after you opened it. Reload it and try again."),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } sql } =>
                (StatusCodes.Status409Conflict, "Already exists", DescribeDuplicate(sql.Message)),
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Value.Status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = problem.Value.Status, Title = problem.Value.Title, Detail = problem.Value.Detail },
        });
    }

    private static string DescribeDuplicate(string sqlMessage) =>
        UniqueIndexMessages.FirstOrDefault(pair => sqlMessage.Contains(pair.Key, StringComparison.Ordinal)).Value
        ?? "A record with the same unique value already exists.";
}
