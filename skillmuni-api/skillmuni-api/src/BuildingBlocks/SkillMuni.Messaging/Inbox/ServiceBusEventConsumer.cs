using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Messaging.Publishing;

namespace SkillMuni.Messaging.Inbox;

/// <summary>
/// Reads this service's subscription on the shared topic. A message is completed once handled (or recognised as a
/// duplicate); a failure abandons it so Service Bus retries and, after the delivery limit, dead-letters it.
/// </summary>
public sealed class ServiceBusEventConsumer<TContext>(
    ServiceBusClientProvider clientProvider,
    IntegrationEventDispatcher<TContext> dispatcher,
    EventHandlerRegistry registry,
    IOptions<ServiceBusOptions> options,
    ILogger<ServiceBusEventConsumer<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (clientProvider.Client is null || string.IsNullOrWhiteSpace(settings.Subscription) || registry.EventTypes.Count == 0)
        {
            logger.LogInformation("Event consumer not started: Service Bus or subscription not configured");
            return;
        }

        _processor = clientProvider.Client.CreateProcessor(settings.Topic, settings.Subscription, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = settings.MaxConcurrentCalls,
        });
        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;
        await _processor.StartProcessingAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        var message = args.Message;
        var tenantId = message.ApplicationProperties.TryGetValue("tenantId", out var tenant) ? tenant as string : null;
        if (string.IsNullOrEmpty(tenantId) || !Guid.TryParse(message.MessageId, out var eventId))
        {
            logger.LogWarning("Dead-lettering {EventType} {MessageId}: missing tenant or event id", message.Subject, message.MessageId);
            await args.DeadLetterMessageAsync(message, "invalid-envelope", cancellationToken: args.CancellationToken);
            return;
        }

        try
        {
            var outcome = await dispatcher.DispatchAsync(message.Subject, eventId, tenantId, message.Body.ToString(), args.CancellationToken);
            logger.LogDebug("Event {EventType} {EventId} {Outcome}", message.Subject, eventId, outcome);
            await args.CompleteMessageAsync(message, args.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Handling {EventType} {EventId} failed (delivery {DeliveryCount})", message.Subject, eventId, message.DeliveryCount);
            await args.AbandonMessageAsync(message, cancellationToken: args.CancellationToken);
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus error on {EntityPath} ({ErrorSource})", args.EntityPath, args.ErrorSource);
        return Task.CompletedTask;
    }
}
