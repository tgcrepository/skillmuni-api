using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Platform.Application;
using Platform.Domain;
using SkillMuni.Common.Paging;

namespace Platform.Infrastructure.Persistence;

internal sealed class TenantThemeRepository(PlatformDbContext db) : ITenantThemeRepository
{
    public Task<TenantTheme?> FindAsync(string tenantId, CancellationToken cancellationToken) =>
        db.Themes.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);
}

/// <summary>Themes change rarely and every app start reads one: cached in Redis per tenant (CLAUDE.md §8).</summary>
internal sealed class CachedTenantThemeRepository(TenantThemeRepository inner, IDistributedCache cache, IOptions<PlatformOptions> options) : ITenantThemeRepository
{
    public async Task<TenantTheme?> FindAsync(string tenantId, CancellationToken cancellationToken)
    {
        var key = $"theme:{tenantId}";
        if (await cache.GetStringAsync(key, cancellationToken) is { } cached)
        {
            return cached.Length == 0 ? null : FromCache(tenantId, cached);
        }

        var theme = await inner.FindAsync(tenantId, cancellationToken);
        var value = theme is null ? string.Empty : JsonSerializer.Serialize(new CachedTheme(theme.Name, theme.LogoUrl, theme.BrandJson, theme.AccentJson, theme.UpdatedAt));
        await cache.SetStringAsync(key, value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = options.Value.CacheFor }, cancellationToken);
        return theme;
    }

    private static TenantTheme? FromCache(string tenantId, string json) =>
        JsonSerializer.Deserialize<CachedTheme>(json) is { } t ? TenantTheme.Create(tenantId, t.Name, t.LogoUrl, t.Brand, t.Accent, t.UpdatedAt) : null;

    private sealed record CachedTheme(string Name, string? LogoUrl, string Brand, string Accent, DateTime UpdatedAt);
}

internal sealed class NotificationRepository(PlatformDbContext db) : INotificationRepository
{
    public async Task<PagedResult<Notification>> ListAsync(Guid learnerId, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.LearnerId == learnerId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(n => n.CreatedAt).Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<Notification>(items, page.Page, page.PageSize, total);
    }

    public Task<int> CountUnreadAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Notifications.AsNoTracking().CountAsync(n => n.LearnerId == learnerId && n.ReadAt == null, cancellationToken);

    public Task<Notification?> GetAsync(Guid learnerId, Guid id, CancellationToken cancellationToken) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.LearnerId == learnerId, cancellationToken);

    public Task<bool> ExistsAsync(Guid learnerId, string sourceKey, CancellationToken cancellationToken) =>
        db.Notifications.AsNoTracking().AnyAsync(n => n.LearnerId == learnerId && n.SourceKey == sourceKey, cancellationToken);

    public void Add(Notification notification) => db.Notifications.Add(notification);
}

internal sealed class PreferencesRepository(PlatformDbContext db) : IPreferencesRepository
{
    public Task<NotificationPreferences?> GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Preferences.FirstOrDefaultAsync(p => p.Id == learnerId, cancellationToken);

    public void Add(NotificationPreferences preferences) => db.Preferences.Add(preferences);
}

internal sealed class DeliveryRepository(PlatformDbContext db) : IDeliveryRepository
{
    public Task<bool> ExistsAsync(string queueMessageId, CancellationToken cancellationToken) =>
        db.Deliveries.AsNoTracking().AnyAsync(d => d.QueueMessageId == queueMessageId, cancellationToken);

    public void Add(NotificationDelivery delivery) => db.Deliveries.Add(delivery);
}

/// <summary>MISS-08. Delivery records hold no learner id (only a masked recipient), so they stay.</summary>
internal sealed class PlatformDataEraser(PlatformDbContext db) : IPlatformDataEraser
{
    public Task EraseAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(
            async ct =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.Notifications.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.Preferences.Where(x => x.Id == learnerId).ExecuteDeleteAsync(ct);
                await transaction.CommitAsync(ct);
            },
            cancellationToken);
}
