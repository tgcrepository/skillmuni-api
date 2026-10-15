using Platform.Application;
using Platform.Domain;
using SkillMuni.Common.Paging;
using SkillMuni.Common.Tenancy;

namespace Platform.UnitTests;

/// <summary>Every repository in memory.</summary>
public sealed class PlatformStore : INotificationRepository, IPreferencesRepository, IDeliveryRepository, ITenantThemeRepository, IPostalDirectory, IUnitOfWork, ITenantContext
{
    public List<Notification> Notifications { get; } = [];

    public List<NotificationPreferences> Preferences { get; } = [];

    public List<NotificationDelivery> Deliveries { get; } = [];

    public List<TenantTheme> Themes { get; } = [];

    public int Saves { get; private set; }

    public string TenantId { get; set; } = "skillmuni-in";

    public string Market { get; set; } = "IN";

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public Task<PagedResult<Notification>> ListAsync(Guid learnerId, PageRequest page, CancellationToken cancellationToken)
    {
        var mine = Notifications.Where(n => n.LearnerId == learnerId).OrderByDescending(n => n.CreatedAt).ToList();
        return Task.FromResult(new PagedResult<Notification>(mine.Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, mine.Count));
    }

    public Task<int> CountUnreadAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Notifications.Count(n => n.LearnerId == learnerId && !n.IsRead));

    public Task<Notification?> GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Notifications.FirstOrDefault(n => n.LearnerId == learnerId && n.Id == id));

    public Task<bool> ExistsAsync(Guid learnerId, string sourceKey, CancellationToken cancellationToken) =>
        Task.FromResult(Notifications.Any(n => n.LearnerId == learnerId && n.SourceKey == sourceKey));

    public void Add(Notification notification) => Notifications.Add(notification);

    public Task<NotificationPreferences?> GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Preferences.FirstOrDefault(p => p.Id == learnerId));

    public void Add(NotificationPreferences preferences) => Preferences.Add(preferences);

    public Task<bool> ExistsAsync(string queueMessageId, CancellationToken cancellationToken) =>
        Task.FromResult(Deliveries.Any(d => d.QueueMessageId == queueMessageId));

    public void Add(NotificationDelivery delivery) => Deliveries.Add(delivery);

    public Task<TenantTheme?> FindAsync(string tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(Themes.FirstOrDefault(t => t.TenantId == tenantId));

    public Task<PostalEntry?> FindAsync(string market, string code, CancellationToken cancellationToken) =>
        Task.FromResult(market == "IN" && code == "560038"
            ? new PostalEntry("Karnataka", "Bengaluru Urban", "Bengaluru", ["Indiranagar", "Domlur"])
            : null);

    public Task<IReadOnlyList<string>> RegionsAsync(string market, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(market == "ZA" ? ["Gauteng", "Western Cape"] : []);
}

/// <summary>Records what it was asked to send.</summary>
public sealed class RecordingSender(string channel) : INotificationSender
{
    public List<OutgoingNotification> Sent { get; } = [];

    public string Channel => channel;

    public Task<SendReceipt> SendAsync(OutgoingNotification notification, CancellationToken cancellationToken)
    {
        Sent.Add(notification);
        return Task.FromResult(new SendReceipt("recording", $"r-{Sent.Count}"));
    }
}
