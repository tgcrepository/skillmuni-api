using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Tenancy;

namespace SkillMuni.Messaging.Inbox;

public enum DispatchOutcome
{
    Handled,
    Duplicate,
    Ignored,
}

/// <summary>
/// Runs the handler for one received event inside its own scope, with the event's tenant applied to the query
/// filters and the inbox row saved in the same transaction as the handler's changes.
/// </summary>
/// <remarks>
/// Several events for one learner are handled at once (MaxConcurrentCalls). When a save loses a race (an optimistic
/// concurrency token, or two handlers inserting the same row), the handler runs again in a fresh scope on the
/// current data, a few times, before the message goes back to Service Bus; otherwise a busy learner's events could
/// use up their delivery count and be dead-lettered.
/// </remarks>
public sealed class IntegrationEventDispatcher<TContext>(IServiceScopeFactory scopes, EventHandlerRegistry registry, TimeProvider clock)
    where TContext : DbContext
{
    public const int MaxAttempts = 4;

    public async Task<DispatchOutcome> DispatchAsync(
        string eventType,
        Guid eventId,
        string tenantId,
        string body,
        CancellationToken cancellationToken)
    {
        if (!registry.TryGet(eventType, out var handler))
        {
            return DispatchOutcome.Ignored;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await DispatchOnceAsync(handler, eventType, eventId, tenantId, body, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt * attempt), cancellationToken);
            }
        }
    }

    private async Task<DispatchOutcome> DispatchOnceAsync(
        Func<IServiceProvider, string, CancellationToken, Task> handler,
        string eventType,
        Guid eventId,
        string tenantId,
        string body,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        if (await db.Set<InboxMessage>().AsNoTracking().AnyAsync(m => m.Id == eventId, cancellationToken))
        {
            return DispatchOutcome.Duplicate;
        }

        db.Set<InboxMessage>().Add(new InboxMessage(eventId, eventType, clock.GetUtcNow().UtcDateTime));
        await handler(scope.ServiceProvider, body, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another replica may have handled it first; if so its transaction won and this one rolled back.
            if (await db.Set<InboxMessage>().AsNoTracking().AnyAsync(m => m.Id == eventId, CancellationToken.None))
            {
                return DispatchOutcome.Duplicate;
            }

            throw;
        }

        return DispatchOutcome.Handled;
    }
}
