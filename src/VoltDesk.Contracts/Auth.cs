using System.ComponentModel.DataAnnotations;

namespace VoltDesk.Contracts;

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, EmployeeDto Employee);
