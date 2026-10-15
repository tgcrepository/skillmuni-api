using Platform.Domain;
using SkillMuni.Common.Paging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Platform.Application.Services;

/// <summary>MISS-06: the bell (list, mark read) and channel preferences. The app doesn't call these yet.</summary>
public interface INotificationService
{
    Task<NotificationPageDto> ListAsync(Guid learnerId, PageRequest page, CancellationToken cancellationToken);

    Task<Result> MarkReadAsync(Guid learnerId, Guid id, CancellationToken cancellationToken);

    Task<NotificationPreferencesDto> PreferencesAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<NotificationPreferencesDto> SavePreferencesAsync(Guid learnerId, UpdatePreferencesRequest request, CancellationToken cancellationToken);

    /// <summary>Adds an in-app notification once per <paramref name="sourceKey"/>; the caller saves.</summary>
    Task NotifyAsync(Guid learnerId, string kind, string title, string body, string? link, string sourceKey, CancellationToken cancellationToken);
}

public sealed class NotificationService(
    INotificationRepository notifications,
    IPreferencesRepository preferences,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock) : INotificationService
{
    public async Task<NotificationPageDto> ListAsync(Guid learnerId, PageRequest page, CancellationToken cancellationToken)
    {
        var list = await notifications.ListAsync(learnerId, page, cancellationToken);
        var unread = await notifications.CountUnreadAsync(learnerId, cancellationToken);
        return new NotificationPageDto(
            list.Items.Select(n => new NotificationDto(n.Id.ToString(), n.Kind, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt)).ToList(),
            list.Page,
            list.PageSize,
            list.Total,
            unread);
    }

    public async Task<Result> MarkReadAsync(Guid learnerId, Guid id, CancellationToken cancellationToken)
    {
        var notification = await notifications.GetAsync(learnerId, id, cancellationToken);
        if (notification is null)
        {
            return PlatformErrors.NotificationNotFound;
        }

        notification.MarkRead(clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<NotificationPreferencesDto> PreferencesAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var saved = await preferences.GetAsync(learnerId, cancellationToken)
            ?? NotificationPreferences.Defaults(tenant.TenantId, learnerId, clock.GetUtcNow().UtcDateTime);
        return ToDto(saved);
    }

    public async Task<NotificationPreferencesDto> SavePreferencesAsync(Guid learnerId, UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var saved = await preferences.GetAsync(learnerId, cancellationToken);
        if (saved is null)
        {
            saved = NotificationPreferences.Defaults(tenant.TenantId, learnerId, now);
            preferences.Add(saved);
        }

        saved.Update(request.Push!.Value, request.WhatsApp!.Value, request.Sms!.Value, request.Email!.Value, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(saved);
    }

    public async Task NotifyAsync(Guid learnerId, string kind, string title, string body, string? link, string sourceKey, CancellationToken cancellationToken)
    {
        if (await notifications.ExistsAsync(learnerId, sourceKey, cancellationToken))
        {
            return;
        }

        notifications.Add(Notification.Create(tenant.TenantId, learnerId, kind, title, body, link, sourceKey, clock.GetUtcNow().UtcDateTime));
    }

    private static NotificationPreferencesDto ToDto(NotificationPreferences p) => new(p.Push, p.WhatsApp, p.Sms, p.Email);
}
