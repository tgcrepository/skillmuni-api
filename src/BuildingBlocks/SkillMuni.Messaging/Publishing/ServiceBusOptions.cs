namespace SkillMuni.Messaging.Publishing;

/// <summary>
/// Service Bus settings. In Azure, set <see cref="FullyQualifiedNamespace"/> and the app signs in with its
/// managed identity. Locally, <see cref="ConnectionString"/> points at the emulator. Neither = events are logged, not sent.
/// </summary>
public sealed class ServiceBusOptions
{
    public const string Section = "ServiceBus";

    public string? ConnectionString { get; set; }

    public string? FullyQualifiedNamespace { get; set; }

    /// <summary>One topic for all cross-service events; subscriptions filter on the message subject.</summary>
    public string Topic { get; set; } = "skillmuni-events";

    /// <summary>This service's subscription on <see cref="Topic"/>; empty = the service consumes no events.</summary>
    public string? Subscription { get; set; }

    public int MaxConcurrentCalls { get; set; } = 4;

    /// <summary>Queue the Platform service reads to send SMS / email / push.</summary>
    public string NotificationsQueue { get; set; } = "notifications";
}
