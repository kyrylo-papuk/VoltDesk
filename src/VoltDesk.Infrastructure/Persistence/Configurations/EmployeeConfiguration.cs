using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoltDesk.Core.Employees;

namespace VoltDesk.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.Property(e => e.FullName).HasMaxLength(Employee.FullNameMaxLength);
        builder.Property(e => e.Email).HasMaxLength(Employee.EmailMaxLength);
        builder.HasIndex(e => e.Email).IsUnique();
        builder.Property(e => e.PasswordHash).HasMaxLength(512).IsUnicode(false);
        builder.HasTrackingColumns();
    }
}
