using Platform.Application.Services;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Privacy;

namespace Platform.Application.Handlers;

/// <summary>A badge becomes a bell notification (once per badge). The dispatcher saves it with the inbox row.</summary>
public sealed class BadgeEarnedHandler(INotificationService notifications) : IIntegrationEventHandler<BadgeEarned>
{
    public Task HandleAsync(BadgeEarned e, CancellationToken cancellationToken) =>
        notifications.NotifyAsync(
            e.LearnerId,
            "badge",
            $"Badge unlocked: {e.BadgeName}",
            "Pin it to your showcase from My Stats · Badges.",
            "/stats/badges",
            $"badge:{e.BadgeId}",
            cancellationToken);
}

/// <summary>MISS-08: notifications and preferences go; a tombstone stops a later request or event recreating them.</summary>
public sealed class LearnerErasedHandler(IPlatformDataEraser eraser, IErasureTombstones tombstones) : IIntegrationEventHandler<LearnerErased>
{
    public async Task HandleAsync(LearnerErased e, CancellationToken cancellationToken)
    {
        await eraser.EraseAsync(e.AccountId, cancellationToken);
        await tombstones.MarkErasedAsync(e.AccountId, cancellationToken);
    }
}
