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
public sealed class IntegrationEventDispatcher<TContext>(IServiceScopeFactory scopes, EventHandlerRegistry registry, TimeProvider clock)
    where TContext : DbContext
{
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
