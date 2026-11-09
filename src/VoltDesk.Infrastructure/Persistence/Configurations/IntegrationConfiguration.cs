using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoltDesk.Core.Projects;
using VoltDesk.Infrastructure.Integrations;

namespace VoltDesk.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.Property(m => m.Type).HasMaxLength(64).IsUnicode(false);
        builder.Property(m => m.LastError).HasMaxLength(1000);

        // The worker asks for pending messages that are due.
        builder.HasIndex(m => new { m.ProcessedAt, m.FailedAt, m.NextAttemptAt });
    }
}

internal sealed class ExternalLinkConfiguration : IEntityTypeConfiguration<ExternalLink>
{
    public void Configure(EntityTypeBuilder<ExternalLink> builder)
    {
        builder.Property(l => l.Kind).HasMaxLength(32).IsUnicode(false);
        builder.Property(l => l.Key).HasMaxLength(100);
        builder.Property(l => l.ExternalId).HasMaxLength(200).IsUnicode(false);
        builder.Property(l => l.Url).HasMaxLength(500);
        builder.HasIndex(l => new { l.Kind, l.ProjectId, l.Key }).IsUnique();

        // SQL Server leaves NULL out of the index above, so links without a project (the root
        // folder) get their own: two concurrent requests cannot both create a root folder.
        builder.HasIndex(l => new { l.Kind, l.Key }).IsUnique().HasFilter("[ProjectId] IS NULL");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(l => l.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
