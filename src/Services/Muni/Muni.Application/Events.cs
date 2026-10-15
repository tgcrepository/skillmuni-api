using SkillMuni.Common.Messaging;

namespace Muni.Application;

// ---- Consumed (Muni's own copies; publisher field names, docs/EVENTS.md). Muni publishes nothing. ----

public sealed record LearnerRegistered(Guid AccountId, string FirstName, string LastName, string Market) : IntegrationEvent
{
    public const string Name = "identity.learner.registered";

    public override string EventType => Name;
}

public sealed record LearnerErased(Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.learner.erased";

    public override string EventType => Name;
}

public sealed record XpAwarded(Guid LearnerId, int TotalXp, int Level, string Rank, int LevelStartXp, int NextLevelXp, int Badges, int StreakDays)
    : IntegrationEvent
{
    public const string Name = "gamification.xp.awarded";

    public override string EventType => Name;
}

public sealed record QuestCompleted(Guid LearnerId, string Slug, int Quest, string Badge) : IntegrationEvent
{
    public const string Name = "learner.quest.completed";

    public override string EventType => Name;
}

public sealed record AssessmentSubmitted(Guid LearnerId, int Attempt, string? ArchetypeName) : IntegrationEvent
{
    public const string Name = "assessment.attempt.submitted";

    public override string EventType => Name;
}
