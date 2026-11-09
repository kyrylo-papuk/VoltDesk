using System.Text.Json;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Infrastructure.Integrations;

/// <summary>Something other systems should hear about. Delivered later from the outbox.</summary>
public interface IIntegrationEvent;

/// <summary>A project was created; identified by number because its id is not known before saving.</summary>
public sealed record ProjectCreated(string ProjectNumber) : IIntegrationEvent;

/// <summary>Milestone due dates or completion changed.</summary>
public sealed record MilestonesChanged(int ProjectId) : IIntegrationEvent;

public interface IIntegrationEvents
{
    /// <summary>Queues the event in the current unit of work; it is stored with the next SaveChanges.</summary>
    void Publish(IIntegrationEvent integrationEvent);
}

/// <summary>Writes events to the outbox table, in the same transaction as the change that caused them.</summary>
internal sealed class OutboxIntegrationEvents(VoltDeskDbContext db, TimeProvider clock) : IIntegrationEvents
{
    public void Publish(IIntegrationEvent integrationEvent)
    {
        var now = clock.GetUtcNow();
        db.Outbox.Add(new OutboxMessage
        {
            Type = integrationEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            CreatedAt = now,
            NextAttemptAt = now,
        });
    }
}

/// <summary>Used when no integration is configured: events go nowhere.</summary>
internal sealed class NoIntegrationEvents : IIntegrationEvents
{
    public void Publish(IIntegrationEvent integrationEvent)
    {
    }
}
