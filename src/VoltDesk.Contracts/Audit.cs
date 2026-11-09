namespace VoltDesk.Contracts;

/// <summary>One saved change. <see cref="Changes"/> maps property names to old and new values.</summary>
public sealed record AuditEntryDto(
    long Id,
    DateTimeOffset OccurredAt,
    int? ActorId,
    string? ActorName,
    string EntityType,
    int EntityId,
    string Action,
    IReadOnlyDictionary<string, AuditValueChange> Changes);

public sealed record AuditValueChange(string? Old, string? New);
