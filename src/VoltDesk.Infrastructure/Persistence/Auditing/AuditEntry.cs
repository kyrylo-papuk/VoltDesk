namespace VoltDesk.Infrastructure.Persistence.Auditing;

/// <summary>
/// One saved change of one entity. <see cref="RootType"/> and <see cref="RootId"/> point to the
/// aggregate the entity belongs to, so the history of a project includes its tasks and milestones.
/// </summary>
public sealed class AuditEntry
{
    public long Id { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public int? ActorId { get; init; }

    public required string EntityType { get; init; }

    public int EntityId { get; init; }

    public required string RootType { get; init; }

    public int RootId { get; init; }

    public required string Action { get; init; }

    /// <summary>JSON object: property name → { "Old": ..., "New": ... }.</summary>
    public required string Changes { get; init; }
}
