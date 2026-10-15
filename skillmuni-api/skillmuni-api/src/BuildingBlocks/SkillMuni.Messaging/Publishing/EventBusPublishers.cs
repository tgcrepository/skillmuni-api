using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Messaging.Outbox;

namespace SkillMuni.Messaging.Publishing;

public interface IEventBusPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}

/// <summary>Sends events to the shared topic. Subject = event type, MessageId = event id (for duplicate detection).</summary>
public sealed class ServiceBusEventPublisher(ServiceBusClient client, IOptions<ServiceBusOptions> options) : IEventBusPublisher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender = client.CreateSender(options.Value.Topic);

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var busMessage = new ServiceBusMessage(BinaryData.FromString(message.Payload))
        {
            MessageId = message.Id.ToString(),
            Subject = message.EventType,
            ContentType = "application/json",
            CorrelationId = message.CorrelationId,
        };
        busMessage.ApplicationProperties["tenantId"] = message.TenantId;
        return _sender.SendMessageAsync(busMessage, cancellationToken);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}

/// <summary>Used when Service Bus isn't configured (unit tests, a laptop without the emulator). Never logs payloads.</summary>
public sealed class LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) : IEventBusPublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Event {EventType} {EventId} recorded; Service Bus is not configured", message.EventType, message.Id);
        return Task.CompletedTask;
    }
}
