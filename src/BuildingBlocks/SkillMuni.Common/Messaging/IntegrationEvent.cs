using System.Text.Json.Serialization;

namespace SkillMuni.Common.Messaging;

/// <summary>
/// A past-tense fact one service tells the others, named <c>&lt;service&gt;.&lt;entity&gt;.&lt;verb&gt;</c>
/// (CLAUDE.md §9). Consumers must be idempotent on <see cref="EventId"/>.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public required string TenantId { get; init; }

    /// <summary>When the fact happened (UTC). Set by the outbox when the publisher leaves it empty.</summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>Sent as the Service Bus message subject, not in the body.</summary>
    [JsonIgnore]
    public abstract string EventType { get; }
}

/// <summary>
/// Queues an event in the current unit of work. It is saved in the same transaction as the change it describes
/// and published afterwards by the outbox dispatcher (SkillMuni.Messaging).
/// </summary>
public interface IOutbox
{
    void Add(IntegrationEvent integrationEvent);
}

/// <summary>Handles one event type a service subscribes to. Must be idempotent (the inbox also dedupes).</summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
