using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Messaging.Publishing;

namespace SkillMuni.Messaging.Outbox;

/// <summary>
/// Publishes pending outbox rows to Service Bus. Delivery is at-least-once: with several replicas an event
/// can go out twice, which is why consumers must be idempotent (CLAUDE.md §9).
/// </summary>
public sealed class OutboxDispatcher<TContext>(
    IServiceScopeFactory scopes,
    IEventBusPublisher publisher,
    TimeProvider clock,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var sent = await DispatchBatchAsync(settings, stoppingToken);
                if (sent < settings.BatchSize)
                {
                    await Task.Delay(settings.PollInterval, clock, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Outbox dispatch failed for {Context}", typeof(TContext).Name);
                await Task.Delay(settings.PollInterval, clock, stoppingToken);
            }
        }
    }

    private async Task<int> DispatchBatchAsync(OutboxOptions settings, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var maxAttempts = settings.MaxAttempts;
        var batchSize = settings.BatchSize;

        var batch = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null && m.Attempts < maxAttempts)
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.MarkProcessed(clock.GetUtcNow().UtcDateTime);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.MarkFailed(ex.Message);
                logger.LogWarning(ex, "Publishing {EventType} {EventId} failed", message.EventType, message.Id);
            }
        }

        if (batch.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return batch.Count;
    }
}
