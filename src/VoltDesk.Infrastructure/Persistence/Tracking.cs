using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VoltDesk.Core.Clients;
using VoltDesk.Core.Panels;
using VoltDesk.Core.Projects;

namespace VoltDesk.Infrastructure.Persistence;

/// <summary>
/// Shadow columns kept out of the domain model: a SQL Server rowversion for optimistic
/// concurrency and created/updated timestamps.
/// </summary>
public static class Tracking
{
    public const string RowVersion = nameof(RowVersion);
    public const string CreatedAt = nameof(CreatedAt);
    public const string UpdatedAt = nameof(UpdatedAt);

    internal static void HasTrackingColumns<T>(this EntityTypeBuilder<T> builder)
        where T : class
    {
        builder.Property<byte[]>(RowVersion).IsRowVersion();
        builder.Property<DateTimeOffset>(CreatedAt);
        builder.Property<DateTimeOffset>(UpdatedAt);
    }

    /// <summary>
    /// Sets timestamps and touches aggregate roots whose children changed. Touching updates the
    /// root row, so its rowversion is checked and bumped: a stale edit of a contact or a milestone
    /// is rejected just like a stale edit of the client or project itself. Circuits touch their panel.
    /// </summary>
    internal static void Stamp(ChangeTracker tracker, DateTimeOffset now)
    {
        foreach (var entry in tracker.Entries().ToList())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached)
            {
                continue;
            }

            switch (entry.Entity)
            {
                case Contact contact:
                    Touch(tracker.Entries<Client>().FirstOrDefault(c => c.Entity.Id == contact.ClientId), now);
                    break;
                case Circuit circuit:
                    Touch(tracker.Entries<ElectricalPanel>().FirstOrDefault(p => p.Entity.Id == circuit.PanelId), now);
                    break;
                case ProjectMilestone when entry.State == EntityState.Modified:
                    var projectId = (int)entry.Property("ProjectId").CurrentValue!;
                    Touch(tracker.Entries<Project>().FirstOrDefault(p => p.Entity.Id == projectId), now);
                    break;
            }

            if (entry.Metadata.FindProperty(UpdatedAt) is null)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Property(CreatedAt).CurrentValue = now;
                entry.Property(UpdatedAt).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(UpdatedAt).CurrentValue = now;
            }
        }
    }

    /// <summary>Sets the version the client last saw; SaveChanges fails if the row changed since.</summary>
    public static void ExpectVersion(this EntityEntry entry, string version)
    {
        var bytes = new byte[8];
        if (!Convert.TryFromBase64String(version, bytes, out var written) || written != bytes.Length)
        {
            throw new InvalidVersionException(version);
        }

        entry.Property(RowVersion).OriginalValue = bytes;
    }

    public static string Version(this EntityEntry entry) =>
        Convert.ToBase64String((byte[])entry.Property(RowVersion).CurrentValue!);

    private static void Touch(EntityEntry? root, DateTimeOffset now)
    {
        if (root is { State: EntityState.Unchanged or EntityState.Modified })
        {
            root.Property(UpdatedAt).CurrentValue = now;
        }
    }
}
