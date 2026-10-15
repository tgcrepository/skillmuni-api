using SkillMuni.Common.Messaging;

namespace Gamification.Application;

// ---- Published ----

/// <summary>The learner's totals after an award (Learner keeps a copy for its screens). Totals, not deltas.</summary>
public sealed record XpAwarded(Guid LearnerId, int TotalXp, int Level, string Rank, int LevelStartXp, int NextLevelXp, int Badges, int StreakDays)
    : IntegrationEvent
{
    public const string Name = "gamification.xp.awarded";

    public override string EventType => Name;
}

public sealed record BadgeEarned(Guid LearnerId, string BadgeId, string BadgeName) : IntegrationEvent
{
    public const string Name = "gamification.badge.earned";

    public override string EventType => Name;
}

// ---- Consumed (Gamification's own copies; publisher field names) ----

public sealed record AccountRegistered(Guid AccountId, string FirstName, string LastName, string Market) : IntegrationEvent
{
    public const string Name = "identity.learner.registered";

    public override string EventType => Name;
}

public sealed record ClaimConfirmed(Guid ClaimId, Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.claim.confirmed";

    public override string EventType => Name;
}

public sealed record AccountErased(Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.learner.erased";

    public override string EventType => Name;
}

public sealed record QuestStepCompleted(Guid LearnerId, string Slug, int Quest, int Part) : IntegrationEvent
{
    public const string Name = "learner.quest.step-completed";

    public override string EventType => Name;
}

public sealed record QuestCompleted(Guid LearnerId, string Slug, int Quest, string Badge) : IntegrationEvent
{
    public const string Name = "learner.quest.completed";

    public override string EventType => Name;
}

public sealed record ProfileTaskCompleted(Guid LearnerId, string Task) : IntegrationEvent
{
    public const string Name = "learner.profile.task-completed";

    public override string EventType => Name;
}

public sealed record ProgrammeEnrolled(Guid LearnerId, string ProgrammeCode, bool PaysStipend) : IntegrationEvent
{
    public const string Name = "learner.programme.enrolled";

    public override string EventType => Name;
}

public sealed record FeedbackSent(Guid LearnerId, int Mood, bool EarnsXp) : IntegrationEvent
{
    public const string Name = "learner.feedback.sent";

    public override string EventType => Name;
}

public sealed record PhotoChanged(Guid LearnerId, string PhotoUrl) : IntegrationEvent
{
    public const string Name = "learner.photo.changed";

    public override string EventType => Name;
}

/// <summary>From Assessment (expected payload, as Learner expects it too).</summary>
public sealed record AssessmentSubmitted(Guid LearnerId, int Attempt, string ArchetypeName, string ArchetypeSummary) : IntegrationEvent
{
    public const string Name = "assessment.attempt.submitted";

    public override string EventType => Name;
}

/// <summary>From Learning (expected payload, MISS-04): minutes spent learning.</summary>
public sealed record LearningActivity(Guid LearnerId, int Minutes) : IntegrationEvent
{
    public const string Name = "learning.activity.recorded";

    public override string EventType => Name;
}

/// <summary>From Learning (expected payload): a brief read to the end.</summary>
public sealed record BriefCompleted(Guid LearnerId, string BriefId) : IntegrationEvent
{
    public const string Name = "learning.brief.completed";

    public override string EventType => Name;
}

/// <summary>From Learning (expected payload): a quiz checked. Only the first check pays XP (practice afterwards).</summary>
public sealed record QuizSubmitted(Guid LearnerId, string QuizId, int Correct, int Total, bool FirstAttempt) : IntegrationEvent
{
    public const string Name = "learning.quiz.submitted";

    public override string EventType => Name;
}

/// <summary>From Learning (expected payload).</summary>
public sealed record ModuleCompleted(Guid LearnerId, string ModuleSlug) : IntegrationEvent
{
    public const string Name = "learning.module.completed";

    public override string EventType => Name;
}

/// <summary>From Learning (expected payload).</summary>
public sealed record CourseCompleted(Guid LearnerId, string CourseSlug) : IntegrationEvent
{
    public const string Name = "learning.course.completed";

    public override string EventType => Name;
}
