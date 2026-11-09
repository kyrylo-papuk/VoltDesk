using VoltDesk.Core.Common;

namespace VoltDesk.Core.Employees;

public sealed class Employee : Entity
{
    public const int FullNameMaxLength = 100;
    public const int EmailMaxLength = 254;

    private Employee()
    {
    }

    public string FullName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public EmployeeRole Role { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Password hash produced by the infrastructure layer; null until a password is set.</summary>
    public string? PasswordHash { get; private set; }

    public bool CanManageProjects => IsActive && Role is (EmployeeRole.ProjectManager or EmployeeRole.Admin);

    public static Employee Create(string fullName, string email, EmployeeRole role) => new()
    {
        FullName = Require.Text(fullName, "Full name", FullNameMaxLength),
        Email = Require.Email(email, "Email"),
        Role = Require.Defined(role, "Role"),
        IsActive = true,
    };

    public void ChangeRole(EmployeeRole role) => Role = Require.Defined(role, "Role");

    public void SetPasswordHash(string passwordHash) =>
        PasswordHash = Require.Text(passwordHash, "Password hash", 512);

    /// <summary>Employees are deactivated, never deleted, so project history keeps its references.</summary>
    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
