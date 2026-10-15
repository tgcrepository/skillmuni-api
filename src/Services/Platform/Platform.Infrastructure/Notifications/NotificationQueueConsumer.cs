using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Services;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Publishing;

namespace Platform.Infrastructure.Notifications;

/// <summary>
/// Reads the <c>notifications</c> queue Identity sends codes and links to (B-09). A message is completed once sent
/// (or recognised as a redelivery); a bad body is dead-lettered; a sender failure abandons it so Service Bus retries and,
/// after the delivery limit, dead-letters it. Expired messages (TTL = the code's lifetime) are never sent.
/// </summary>
internal sealed partial class NotificationQueueConsumer(
    ServiceBusClientProvider clientProvider,
    IServiceScopeFactory scopes,
    IOptions<ServiceBusOptions> options,
    ILogger<NotificationQueueConsumer> logger) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (clientProvider.Client is null || string.IsNullOrWhiteSpace(settings.NotificationsQueue))
        {
            LogNotStarted(logger);
            return;
        }

        _processor = clientProvider.Client.CreateProcessor(settings.NotificationsQueue, new ServiceBusProcessorOptions
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

    /// <summary>Handles one message body; also the seam the integration tests use.</summary>
    internal async Task<DispatchResult?> HandleAsync(string body, string? messageId, CancellationToken cancellationToken)
    {
        var message = QueuedNotification.TryParse(body);
        if (message is null)
        {
            return null;
        }

        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(message.TenantId);
        return await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>().DispatchAsync(message, messageId, cancellationToken);
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        var message = args.Message;
        try
        {
            var result = await HandleAsync(message.Body.ToString(), message.MessageId, args.CancellationToken);
            if (result is null)
            {
                LogInvalid(logger, message.Subject, message.MessageId);
                await args.DeadLetterMessageAsync(message, "invalid-message", cancellationToken: args.CancellationToken);
                return;
            }

            await args.CompleteMessageAsync(message, args.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, message.Subject, message.DeliveryCount);
            await args.AbandonMessageAsync(message, cancellationToken: args.CancellationToken);
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        LogBusError(logger, args.Exception, args.EntityPath, args.ErrorSource.ToString());
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification queue consumer not started: Service Bus not configured")]
    private static partial void LogNotStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dead-lettering notification {Template} {MessageId}: unreadable body")]
    private static partial void LogInvalid(ILogger logger, string? template, string? messageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sending notification {Template} failed (delivery {DeliveryCount})")]
    private static partial void LogFailed(ILogger logger, Exception exception, string? template, int deliveryCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service Bus error on {EntityPath} ({ErrorSource})")]
    private static partial void LogBusError(ILogger logger, Exception exception, string entityPath, string errorSource);
}
