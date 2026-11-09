using System.ComponentModel.DataAnnotations;
using VoltDesk.Core.Employees;

namespace VoltDesk.Contracts;

public sealed record EmployeeDto(int Id, string FullName, string Email, EmployeeRole Role, bool IsActive, string Version);

public sealed record CreateEmployeeRequest(
    [Required, StringLength(Employee.FullNameMaxLength)] string FullName,
    [Required, EmailAddress, StringLength(Employee.EmailMaxLength)] string Email,
    EmployeeRole Role,
    [Required, StringLength(128, MinimumLength = 10)] string InitialPassword);

public sealed record ChangeRoleRequest(EmployeeRole Role, [Required] string Version);
