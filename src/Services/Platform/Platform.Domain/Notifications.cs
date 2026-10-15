namespace Platform.Domain;

/// <summary>An in-app notification for the bell (MISS-06). <see cref="SourceKey"/> makes each one unique per learner.</summary>
public sealed class Notification : Entity
{
    public const int MaxTitleLength = 120;
    public const int MaxBodyLength = 500;

    private Notification()
    {
    }

    public Guid LearnerId { get; private set; }

    /// <summary>e.g. <c>badge</c>.</summary>
    public string Kind { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>App route the notification opens.</summary>
    public string? Link { get; private set; }

    /// <summary>What caused it, e.g. <c>badge:quest-who-dis</c>: the same cause never notifies twice.</summary>
    public string SourceKey { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(string tenantId, Guid learnerId, string kind, string title, string body, string? link, string sourceKey, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        Kind = kind,
        Title = Clip(title, MaxTitleLength),
        Body = Clip(body, MaxBodyLength),
        Link = link,
        SourceKey = sourceKey,
        CreatedAt = now,
    };

    public void MarkRead(DateTime now) => ReadAt ??= now;

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}

/// <summary>
/// Which channels a learner wants engagement messages on. Sign-in and verification codes are always sent: they are
/// part of the action the learner asked for. <see cref="Entity.Id"/> is the learner id.
/// </summary>
public sealed class NotificationPreferences : Entity
{
    private NotificationPreferences()
    {
    }

    public bool Push { get; private set; }

    public bool WhatsApp { get; private set; }

    public bool Sms { get; private set; }

    public bool Email { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Opt-out for push and email; WhatsApp and SMS need an explicit opt-in (DPDP / POPIA, WhatsApp policy).</summary>
    public static NotificationPreferences Defaults(string tenantId, Guid learnerId, DateTime now) => new()
    {
        Id = learnerId,
        TenantId = tenantId,
        Push = true,
        WhatsApp = false,
        Sms = false,
        Email = true,
        UpdatedAt = now,
    };

    public void Update(bool push, bool whatsApp, bool sms, bool email, DateTime now)
    {
        Push = push;
        WhatsApp = whatsApp;
        Sms = sms;
        Email = email;
        UpdatedAt = now;
    }
}

public static class DeliveryStatus
{
    public const string Sent = "sent";
    public const string Skipped = "skipped";
}

/// <summary>
/// One SMS / email / push / WhatsApp handed to a provider. Keeps the recipient masked and never the message's secret
/// (codes, links): enough to answer "was it sent?" without holding personal data.
/// </summary>
public sealed class NotificationDelivery : Entity
{
    private NotificationDelivery()
    {
    }

    public string Channel { get; private set; } = string.Empty;

    public string Template { get; private set; } = string.Empty;

    /// <summary>Masked, e.g. <c>***3210</c> or <c>y***@gmail.com</c>.</summary>
    public string Recipient { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public string Provider { get; private set; } = string.Empty;

    public string? ProviderMessageId { get; private set; }

    /// <summary>The queue message it came from, so a redelivered message isn't sent twice.</summary>
    public string? QueueMessageId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static NotificationDelivery Record(
        string tenantId,
        string channel,
        string template,
        string maskedRecipient,
        string status,
        string provider,
        string? providerMessageId,
        string? queueMessageId,
        DateTime now) => new()
        {
            TenantId = tenantId,
            Channel = channel,
            Template = template,
            Recipient = maskedRecipient,
            Status = status,
            Provider = provider,
            ProviderMessageId = providerMessageId,
            QueueMessageId = queueMessageId,
            CreatedAt = now,
        };
}
