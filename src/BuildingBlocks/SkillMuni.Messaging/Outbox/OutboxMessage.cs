using System.Text.Json;
using SkillMuni.Common.Messaging;

namespace SkillMuni.Messaging.Outbox;

/// <summary>
/// An event waiting to be published. It is written in the same transaction as the change it describes,
/// so an event is never lost and never sent for a change that rolled back.
/// </summary>
public sealed class OutboxMessage
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public string TenantId { get; private set; } = string.Empty;

    public string? CorrelationId { get; private set; }

    public DateTime OccurredAt { get; private set; }

    public DateTime? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Create(IntegrationEvent integrationEvent, DateTime occurredAt, string? correlationId) => new()
    {
        Id = integrationEvent.EventId,
        EventType = integrationEvent.EventType,
        Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), Json),
        TenantId = integrationEvent.TenantId,
        CorrelationId = correlationId,
        OccurredAt = occurredAt,
    };

    public void MarkProcessed(DateTime at) => ProcessedAt = at;

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > 1000 ? error[..1000] : error;
    }
}
