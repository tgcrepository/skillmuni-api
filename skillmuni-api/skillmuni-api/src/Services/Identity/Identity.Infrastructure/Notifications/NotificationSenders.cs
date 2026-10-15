using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Identity.Application.Abstractions;
using Microsoft.Extensions.Logging;
using SkillMuni.Common.Privacy;

namespace Identity.Infrastructure.Notifications;

/// <summary>
/// Puts the message on the <c>notifications</c> queue for the Platform service, which owns the SMS / email
/// providers. The message expires with the code, so a stale code is never delivered.
/// </summary>
public sealed class ServiceBusNotificationSender(ServiceBusSender sender) : INotificationSender, IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task SendAsync(CodeNotification notification, CancellationToken cancellationToken)
    {
        var body = new
        {
            notification.TenantId,
            Channel = notification.Channel == NotificationChannel.Sms ? "sms" : "email",
            notification.To,
            notification.Template,
            Data = new { secret = notification.Secret, validForMinutes = notification.ValidForMinutes },
        };
        var message = new ServiceBusMessage(BinaryData.FromString(JsonSerializer.Serialize(body, Json)))
        {
            ContentType = "application/json",
            Subject = notification.Template,
            TimeToLive = TimeSpan.FromMinutes(Math.Max(1, notification.ValidForMinutes)),
        };
        return sender.SendMessageAsync(message, cancellationToken);
    }

    public ValueTask DisposeAsync() => sender.DisposeAsync();
}

/// <summary>Local runs and tests without Service Bus: logs who would get what, masked, and never the code.</summary>
public sealed class LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(CodeNotification notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Notification {Template} via {Channel} to {To} not sent: Service Bus is not configured",
            notification.Template,
            notification.Channel,
            Pii.Contact(notification.To));
        return Task.CompletedTask;
    }
}
