namespace VoltDesk.Infrastructure.Integrations;

/// <summary>An integration event waiting to be delivered, with retry bookkeeping.</summary>
public sealed class OutboxMessage
{
    public long Id { get; init; }

    public required string Type { get; init; }

    public required string Payload { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Set when the message gave up after too many attempts.</summary>
    public DateTimeOffset? FailedAt { get; set; }

    public string? LastError { get; set; }
}

/// <summary>
/// An object created in an external system on behalf of VoltDesk (a Drive folder, a calendar
/// event). Kept outside the aggregates so background sync never bumps their versions.
/// </summary>
public sealed class ExternalLink
{
    public const string RootFolder = nameof(RootFolder);
    public const string ProjectFolder = nameof(ProjectFolder);
    public const string MilestoneEvent = nameof(MilestoneEvent);

    public long Id { get; init; }

    public required string Kind { get; init; }

    public int? ProjectId { get; init; }

    /// <summary>Distinguishes links of the same kind, e.g. the design stage of a milestone event.</summary>
    public string Key { get; init; } = string.Empty;

    public required string ExternalId { get; set; }

    public string? Url { get; set; }
}
