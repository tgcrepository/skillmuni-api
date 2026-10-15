using SkillMuni.Common.Messaging;

namespace Gamification.Application;

// Field names follow docs/EVENTS.md (the JSON contract). Unknown fields are ignored, so publishers can add fields.

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

public sealed record AccountErased(Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.learner.erased";

    public override string EventType => Name;
}

/// <summary><see cref="Paid"/> is false for an all-optional step finished with nothing added (BR-101): it counts, but pays 0.</summary>
public sealed record QuestStepCompleted(Guid LearnerId, string Slug, int Quest, int Part, bool Paid = true) : IntegrationEvent
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

/// <summary>BR-110: the Job Profile reached 100% for the first time.</summary>
public sealed record JobProfileCompleted(Guid LearnerId) : IntegrationEvent
{
    public const string Name = "learner.job-profile.completed";

    public override string EventType => Name;
}

public sealed record ProgrammeEnrolled(Guid LearnerId, string ProgrammeCode, bool PaysStipend) : IntegrationEvent
{
    public const string Name = "learner.programme.enrolled";

    public override string EventType => Name;
}

/// <summary>BR-114: Gamification decides whether it pays (at most once per 30 days).</summary>
public sealed record FeedbackSent(Guid LearnerId, int Mood) : IntegrationEvent
{
    public const string Name = "learner.feedback.sent";

    public override string EventType => Name;
}

public sealed record PhotoChanged(Guid LearnerId, string PhotoUrl) : IntegrationEvent
{
    public const string Name = "learner.photo.changed";

    public override string EventType => Name;
}

/// <summary>From Assessment: the 9th pair of an island answered (BR-104 pays islands 1–3).</summary>
public sealed record IslandCleared(Guid LearnerId, int Attempt, int Island, bool FirstAttempt) : IntegrationEvent
{
    public const string Name = "assessment.island.cleared";

    public override string EventType => Name;
}

/// <summary>From Assessment: Career Drivers submitted (BR-105, pays island 4).</summary>
public sealed record AssessmentSubmitted(
    Guid LearnerId,
    int Attempt,
    bool FirstAttempt,
    string ArchetypeName,
    string ArchetypeSummary,
    IReadOnlyList<string>? TopDrivers) : IntegrationEvent
{
    public const string Name = "assessment.attempt.submitted";

    public override string EventType => Name;
}

/// <summary>From Learning: minutes spent learning (daily quest "Learn for 15 minutes").</summary>
public sealed record LearningActivity(Guid LearnerId, int Minutes) : IntegrationEvent
{
    public const string Name = "learning.activity.recorded";

    public override string EventType => Name;
}

/// <summary>From Learning: a brief read to the end.</summary>
public sealed record BriefCompleted(Guid LearnerId, string BriefId, string? ModuleSlug, string? CourseSlug, bool FirstCompletion) : IntegrationEvent
{
    public const string Name = "learning.brief.completed";

    public override string EventType => Name;
}

/// <summary>From Learning: a brief's quiz submitted. Only the first submit pays and keeps the streak (BR-106, B-45).</summary>
public sealed record QuizSubmitted(Guid LearnerId, string QuizId, string? BriefId, string? ModuleSlug, int Correct, int Total, bool FirstAttempt)
    : IntegrationEvent
{
    public const string Name = "learning.quiz.submitted";

    public override string EventType => Name;
}

public sealed record ModuleCompleted(Guid LearnerId, string ModuleSlug, string? CourseSlug) : IntegrationEvent
{
    public const string Name = "learning.module.completed";

    public override string EventType => Name;
}

public sealed record CourseCompleted(Guid LearnerId, string CourseSlug) : IntegrationEvent
{
    public const string Name = "learning.course.completed";

    public override string EventType => Name;
}

/// <summary>From Learning: the Lobby's first-moves bonus collected (BR-107).</summary>
public sealed record FirstMovesClaimed(Guid LearnerId) : IntegrationEvent
{
    public const string Name = "learning.first-moves.claimed";

    public override string EventType => Name;
}

/// <summary>From Careers: an application sent (BR-111 pays the first).</summary>
public sealed record ApplicationSent(Guid LearnerId, string? ApplicationId, string? JobId) : IntegrationEvent
{
    public const string Name = "careers.application.sent";

    public override string EventType => Name;
}

/// <summary>From Careers: a Hire Fest slot booked (BR-112 pays the first).</summary>
public sealed record FestBooked(Guid LearnerId, string? BookingId, string? FestId) : IntegrationEvent
{
    public const string Name = "careers.fest.booked";

    public override string EventType => Name;
}
