using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using VoltDesk.Contracts;
using VoltDesk.Core.Clients;
using VoltDesk.Core.Employees;
using VoltDesk.Core.Projects;

namespace VoltDesk.Infrastructure.Persistence.Auditing;

/// <summary>
/// Captures changes before SaveChanges and turns them into audit entries afterwards,
/// when database-generated keys of new rows are known.
/// </summary>
internal static class AuditCollector
{
    private static readonly HashSet<string> IgnoredProperties =
        ["Id", Tracking.RowVersion, Tracking.CreatedAt, Tracking.UpdatedAt];

    private static readonly HashSet<string> SecretProperties = [nameof(Employee.PasswordHash)];

    public static List<PendingAudit> Capture(ChangeTracker tracker)
    {
        var pending = new List<PendingAudit>();
        foreach (var entry in tracker.Entries())
        {
            if (entry.Entity is AuditEntry || entry.State is EntityState.Unchanged or EntityState.Detached)
            {
                continue;
            }

            // Milestones are created and removed with their project; only edits are interesting.
            if (entry.Entity is ProjectMilestone && entry.State != EntityState.Modified)
            {
                continue;
            }

            var changes = DescribeChanges(entry);
            if (changes.Count == 0)
            {
                continue;
            }

            pending.Add(new PendingAudit(entry, entry.State, changes, entry.State == EntityState.Deleted ? Keys(entry) : null));
        }

        return pending;
    }

    public static AuditEntry ToEntry(PendingAudit pending, int? actorId, DateTimeOffset now)
    {
        var keys = pending.KeysBeforeSave ?? Keys(pending.Entry);
        return new AuditEntry
        {
            OccurredAt = now,
            ActorId = actorId,
            EntityType = keys.EntityType,
            EntityId = keys.EntityId,
            RootType = keys.RootType,
            RootId = keys.RootId,
            Action = pending.State.ToString(),
            Changes = JsonSerializer.Serialize(pending.Changes),
        };
    }

    private static Dictionary<string, AuditValueChange> DescribeChanges(EntityEntry entry)
    {
        var prefix = entry.Entity is ProjectMilestone milestone ? $"{milestone.Stage.Abbreviation()}." : string.Empty;
        var changes = new Dictionary<string, AuditValueChange>();
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (IgnoredProperties.Contains(name) || property.Metadata.IsForeignKey() && entry.Entity is ProjectMilestone)
            {
                continue;
            }

            var (old, current) = entry.State switch
            {
                EntityState.Added => (null, Format(property.CurrentValue)),
                EntityState.Deleted => (Format(property.OriginalValue), null),
                _ when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue) =>
                    (Format(property.OriginalValue), Format(property.CurrentValue)),
                _ => ((string?)null, (string?)null),
            };
            if (old is null && current is null)
            {
                continue;
            }

            changes[prefix + name] = SecretProperties.Contains(name)
                ? new AuditValueChange(old is null ? null : "***", current is null ? null : "***")
                : new AuditValueChange(old, current);
        }

        return changes;
    }

    private static AuditKeys Keys(EntityEntry entry)
    {
        int Read(string property) => (int)entry.Property(property).CurrentValue!;

        return entry.Entity switch
        {
            ProjectMilestone => new("Project", Read("ProjectId"), "Project", Read("ProjectId")),
            ProjectTask task => new(nameof(ProjectTask), task.Id, nameof(Project), task.ProjectId),
            Contact contact => new(nameof(Contact), contact.Id, nameof(Client), contact.ClientId),
            _ => new(entry.Metadata.ClrType.Name, Read("Id"), entry.Metadata.ClrType.Name, Read("Id")),
        };
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    internal sealed record PendingAudit(
        EntityEntry Entry,
        EntityState State,
        Dictionary<string, AuditValueChange> Changes,
        AuditKeys? KeysBeforeSave);

    internal sealed record AuditKeys(string EntityType, int EntityId, string RootType, int RootId);
}
