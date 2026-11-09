using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoltDesk.Core.Employees;
using VoltDesk.Infrastructure.Persistence.Auditing;

namespace VoltDesk.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.Property(a => a.EntityType).HasMaxLength(64).IsUnicode(false);
        builder.Property(a => a.RootType).HasMaxLength(64).IsUnicode(false);
        builder.Property(a => a.Action).HasMaxLength(16).IsUnicode(false);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(a => a.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        // History screens read one aggregate at a time, newest first.
        builder.HasIndex(a => new { a.RootType, a.RootId, a.OccurredAt });
    }
}
