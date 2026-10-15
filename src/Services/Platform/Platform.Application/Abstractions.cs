using Platform.Domain;
using SkillMuni.Common.Paging;

namespace Platform.Application;

// ---- Reference data (seed data behind provider interfaces, so a real dataset or CMS can replace it) ----

public sealed record PostalEntry(string Region, string District, string City, IReadOnlyList<string> Areas);

/// <summary>PIN / postal codes and regions per market (API-032/033). Phase 1 ships a starter dataset.</summary>
public interface IPostalDirectory
{
    /// <param name="market">Upper-case market code.</param>
    /// <param name="code">Digits only, no spaces.</param>
    Task<PostalEntry?> FindAsync(string market, string code, CancellationToken cancellationToken);

    /// <summary>States / UTs (IN) or provinces (ZA); empty for an unknown market.</summary>
    Task<IReadOnlyList<string>> RegionsAsync(string market, CancellationToken cancellationToken);
}

public interface ITenantThemeRepository
{
    Task<TenantTheme?> FindAsync(string tenantId, CancellationToken cancellationToken);
}

// ---- Notifications (MISS-06) ----

public interface INotificationRepository
{
    Task<PagedResult<Notification>> ListAsync(Guid learnerId, PageRequest page, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid learnerId, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    Task<Notification?> GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid learnerId, string sourceKey, CancellationToken cancellationToken);

    void Add(Notification notification);
}

public interface IPreferencesRepository
{
    /// <summary>Tracked.</summary>
    Task<NotificationPreferences?> GetAsync(Guid learnerId, CancellationToken cancellationToken);

    void Add(NotificationPreferences preferences);
}

public interface IDeliveryRepository
{
    Task<bool> ExistsAsync(string queueMessageId, CancellationToken cancellationToken);

    void Add(NotificationDelivery delivery);
}

/// <summary>A message for one recipient. <see cref="Secret"/> (a code or link) must never be logged or stored.</summary>
public sealed record OutgoingNotification(string TenantId, string Channel, string To, string Template, string? Secret, int ValidForMinutes);

public sealed record SendReceipt(string Provider, string? ProviderMessageId);

/// <summary>
/// Hands a message to the provider for one channel (<c>sms</c>, <c>email</c>, <c>push</c>, <c>whatsapp</c>). Providers are
/// still open (BRD §9); the default is a sandbox sender that logs masked details and sends nothing.
/// </summary>
public interface INotificationSender
{
    string Channel { get; }

    Task<SendReceipt> SendAsync(OutgoingNotification notification, CancellationToken cancellationToken);
}

/// <summary>MISS-08 erasure: every row this service holds for one learner. Safe to run twice.</summary>
public interface IPlatformDataEraser
{
    Task EraseAsync(Guid learnerId, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
