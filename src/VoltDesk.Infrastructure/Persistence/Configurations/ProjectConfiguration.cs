using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoltDesk.Core.Projects;

namespace VoltDesk.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasTrackingColumns();
        builder.Property(p => p.Number).HasMaxLength(Project.NumberMaxLength);
        builder.HasIndex(p => p.Number).IsUnique();
        builder.Property(p => p.Name).HasMaxLength(Project.NameMaxLength);
        builder.Property(p => p.City).HasMaxLength(Project.CityMaxLength);
        builder.Property(p => p.State).HasMaxLength(2).IsFixedLength().IsUnicode(false);
        builder.Property(p => p.Fee).HasPrecision(18, 2);
        builder.HasIndex(p => p.Status);

        // Clients and employees are never deleted while projects reference them.
        builder.HasOne(p => p.Client)
            .WithMany()
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ProjectManager)
            .WithMany()
            .HasForeignKey(p => p.ProjectManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(p => p.Milestones, milestone =>
        {
            milestone.ToTable("ProjectMilestones");
            milestone.WithOwner().HasForeignKey("ProjectId");
            milestone.HasKey("ProjectId", nameof(ProjectMilestone.Stage));
        });

        builder.HasMany(p => p.Tasks)
            .WithOne()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(p => p.CurrentMilestone);
    }
}

internal sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.HasTrackingColumns();
        builder.Property(t => t.Title).HasMaxLength(ProjectTask.TitleMaxLength);
        builder.Property(t => t.Description).HasMaxLength(ProjectTask.DescriptionMaxLength);
        builder.Property(t => t.EstimatedHours).HasPrecision(6, 1);

        builder.HasOne(t => t.Assignee)
            .WithMany()
            .HasForeignKey(t => t.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        // The task board loads tasks of one project grouped by status.
        builder.HasIndex(t => new { t.ProjectId, t.Status });
    }
}
