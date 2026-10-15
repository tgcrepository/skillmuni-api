using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Privacy;

namespace SkillMuni.Messaging.Inbox;

/// <summary>Event type name → how to deserialise it and which handler runs. Built at start-up, no reflection per message.</summary>
public sealed class EventHandlerRegistry
{
    public EventHandlerRegistry(IEnumerable<HandlerRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            registration.Apply(this);
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, Func<IServiceProvider, string, CancellationToken, Task>> _handlers = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> EventTypes => _handlers.Keys;

    public void Register<TEvent, THandler>(string eventType)
        where TEvent : IntegrationEvent
        where THandler : IIntegrationEventHandler<TEvent>
    {
        _handlers[eventType] = async (services, body, cancellationToken) =>
        {
            var integrationEvent = JsonSerializer.Deserialize<TEvent>(body, Json)
                ?? throw new JsonException($"Empty body for {eventType}.");

            // MISS-08: an event about a learner who has since erased their account is recorded (inbox) and skipped,
            // so a late event can't rebuild their rows.
            if (integrationEvent is ILearnerEvent learnerEvent
                && services.GetService<IErasureTombstones>() is { } tombstones
                && await tombstones.IsErasedAsync(learnerEvent.LearnerId, cancellationToken))
            {
                return;
            }

            await services.GetRequiredService<THandler>().HandleAsync(integrationEvent, cancellationToken);
        };
    }

    public bool TryGet(string eventType, out Func<IServiceProvider, string, CancellationToken, Task> handler) =>
        _handlers.TryGetValue(eventType, out handler!);
}

/// <summary>One <c>AddIntegrationEventHandler</c> call, applied when the registry is built.</summary>
public sealed class HandlerRegistration(Action<EventHandlerRegistry> apply)
{
    public void Apply(EventHandlerRegistry registry) => apply(registry);
}
