using Microsoft.Extensions.Logging;
using Platform.Application;
using SkillMuni.Common.Privacy;

namespace Platform.Infrastructure.Notifications;

public static class NotificationChannels
{
    public const string Sms = "sms";
    public const string Email = "email";
    public const string Push = "push";
    public const string WhatsApp = "whatsapp";

    public static readonly string[] All = [Sms, Email, Push, WhatsApp];
}

/// <summary>
/// The default sender for every channel while the SMS / email / push / WhatsApp providers are open (BRD §9). Sends
/// nothing; logs the template, channel and masked recipient. Never the code or link, never the full contact.
/// </summary>
internal sealed partial class SandboxNotificationSender(string channel, ILogger<SandboxNotificationSender> logger) : INotificationSender
{
    public const string Provider = "sandbox";

    public string Channel => channel;

    public Task<SendReceipt> SendAsync(OutgoingNotification notification, CancellationToken cancellationToken)
    {
        LogAccepted(logger, notification.Template, Channel, Pii.Contact(notification.To));
        return Task.FromResult(new SendReceipt(Provider, null));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sandbox accepted {Template} via {Channel} to {To}; nothing was sent")]
    private static partial void LogAccepted(ILogger logger, string template, string channel, string to);
}
