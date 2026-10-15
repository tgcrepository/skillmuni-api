using Microsoft.EntityFrameworkCore;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Tenancy;

namespace SkillMuni.Messaging.Outbox;

/// <summary>Writes the event as an <see cref="OutboxMessage"/> row in the service's own DbContext.</summary>
public sealed class EfOutbox<TContext>(TContext db, TimeProvider clock, ICorrelationContext correlation) : IOutbox
    where TContext : DbContext
{
    public void Add(IntegrationEvent integrationEvent) =>
        db.Set<OutboxMessage>().Add(OutboxMessage.Create(integrationEvent, clock.GetUtcNow().UtcDateTime, correlation.CorrelationId));
}

public sealed class OutboxOptions
{
    public const string Section = "Outbox";

    public bool Enabled { get; set; } = true;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 50;

    public int MaxAttempts { get; set; } = 10;
}
