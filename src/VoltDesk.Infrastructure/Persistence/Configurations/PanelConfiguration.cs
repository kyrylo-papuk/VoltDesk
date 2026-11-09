using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoltDesk.Core.Panels;
using VoltDesk.Core.Projects;

namespace VoltDesk.Infrastructure.Persistence.Configurations;

internal sealed class PanelConfiguration : IEntityTypeConfiguration<ElectricalPanel>
{
    public void Configure(EntityTypeBuilder<ElectricalPanel> builder)
    {
        builder.ToTable("ElectricalPanels");
        builder.HasTrackingColumns();
        builder.Property(p => p.Name).HasMaxLength(ElectricalPanel.NameMaxLength);
        builder.Property(p => p.Location).HasMaxLength(ElectricalPanel.LocationMaxLength);

        // Panel names are unique within a project ("LP-2A" can exist in many projects).
        builder.HasIndex(p => new { p.ProjectId, p.Name }).IsUnique();

        // A panel references its project by id: they are separate aggregates.
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(p => p.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Circuits)
            .WithOne()
            .HasForeignKey(c => c.PanelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CircuitConfiguration : IEntityTypeConfiguration<Circuit>
{
    public void Configure(EntityTypeBuilder<Circuit> builder)
    {
        builder.Property(c => c.Description).HasMaxLength(Circuit.DescriptionMaxLength);
        builder.Property(c => c.LoadVa).HasPrecision(12, 1);

        // No unique index on Slot: rebalancing swaps slots, which a per-row unique check would reject
        // halfway through the batch. The aggregate guarantees slots never overlap.
        builder.HasIndex(c => c.PanelId);
        builder.Ignore(c => c.Slots);
        builder.Ignore(c => c.Label);
    }
}
