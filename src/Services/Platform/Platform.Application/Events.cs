using SkillMuni.Common.Messaging;

namespace Platform.Application;

// ---- Consumed (Platform's own copies; publisher field names, docs/EVENTS.md). Platform publishes nothing. ----

public sealed record LearnerErased(Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.learner.erased";

    public override string EventType => Name;
}

public sealed record BadgeEarned(Guid LearnerId, string BadgeId, string BadgeName) : IntegrationEvent, ILearnerEvent
{
    public const string Name = "gamification.badge.earned";

    public override string EventType => Name;
}
