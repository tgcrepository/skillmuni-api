using Platform.Application.Services;
using SkillMuni.Common.Messaging;

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

public sealed class LearnerErasedHandler(IPlatformDataEraser eraser) : IIntegrationEventHandler<LearnerErased>
{
    public Task HandleAsync(LearnerErased e, CancellationToken cancellationToken) => eraser.EraseAsync(e.AccountId, cancellationToken);
}
