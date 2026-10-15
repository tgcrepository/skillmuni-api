using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Domain;
using SkillMuni.Common.Privacy;

namespace Platform.Application.Services;

/// <summary>
/// One message from the <c>notifications</c> queue, as Identity sends it (B-09):
/// <c>{ tenantId, channel: "sms" | "email", to, template, data: { secret, validForMinutes } }</c>.
/// </summary>
public sealed record QueuedNotification(string TenantId, string Channel, string To, string Template, QueuedNotificationData? Data)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Null when the body isn't a usable message (it is then dead-lettered).</summary>
    public static QueuedNotification? TryParse(string body)
    {
        try
        {
            var message = JsonSerializer.Deserialize<QueuedNotification>(body, Json);
            return message is null
                || string.IsNullOrWhiteSpace(message.TenantId)
                || string.IsNullOrWhiteSpace(message.Channel)
                || string.IsNullOrWhiteSpace(message.Template)
                ? null
                : message;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record QueuedNotificationData(string? Secret, int ValidForMinutes);

public enum DispatchResult
{
    Sent,
    Skipped,
    Duplicate,
}

/// <summary>Sends one queued message through the sender for its channel and records the delivery (masked, no secret).</summary>
public interface INotificationDispatcher
{
    /// <param name="queueMessageId">Service Bus message id, used to ignore a redelivered message.</param>
    Task<DispatchResult> DispatchAsync(QueuedNotification message, string? queueMessageId, CancellationToken cancellationToken);
}

public sealed partial class NotificationDispatcher(
    IEnumerable<INotificationSender> senders,
    IDeliveryRepository deliveries,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public const int MaxQueueMessageIdLength = 128;

    public async Task<DispatchResult> DispatchAsync(QueuedNotification message, string? queueMessageId, CancellationToken cancellationToken)
    {
        var messageId = string.IsNullOrWhiteSpace(queueMessageId) || queueMessageId.Length > MaxQueueMessageIdLength ? null : queueMessageId;
        if (messageId is not null && await deliveries.ExistsAsync(messageId, cancellationToken))
        {
            return DispatchResult.Duplicate;
        }

        var channel = message.Channel.Trim().ToLowerInvariant();
        var masked = Pii.Contact(message.To);
        var now = clock.GetUtcNow().UtcDateTime;
        var sender = senders.FirstOrDefault(s => s.Channel == channel);
        if (sender is null || string.IsNullOrWhiteSpace(message.To))
        {
            LogSkipped(logger, message.Template, channel, masked);
            deliveries.Add(NotificationDelivery.Record(message.TenantId, channel, message.Template, masked, DeliveryStatus.Skipped, "none", null, messageId, now));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return DispatchResult.Skipped;
        }

        var receipt = await sender.SendAsync(
            new OutgoingNotification(message.TenantId, channel, message.To, message.Template, message.Data?.Secret, message.Data?.ValidForMinutes ?? 0),
            cancellationToken);
        deliveries.Add(NotificationDelivery.Record(message.TenantId, channel, message.Template, masked, DeliveryStatus.Sent, receipt.Provider, receipt.ProviderMessageId, messageId, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogSent(logger, message.Template, channel, masked, receipt.Provider);
        return DispatchResult.Sent;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification {Template} via {Channel} to {To} skipped: no sender for the channel or no recipient")]
    private static partial void LogSkipped(ILogger logger, string template, string channel, string to);

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification {Template} via {Channel} to {To} handed to {Provider}")]
    private static partial void LogSent(ILogger logger, string template, string channel, string to, string provider);
}
