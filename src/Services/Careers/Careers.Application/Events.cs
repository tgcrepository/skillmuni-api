using SkillMuni.Common.Messaging;

namespace Careers.Application;

// ---- Published (docs/EVENTS.md) ----

/// <summary>Gamification pays +100 on the first one (BR-111, key <c>mission:first-application</c>).</summary>
public sealed record ApplicationSent(Guid LearnerId, Guid ApplicationId, string JobId) : IntegrationEvent
{
    public const string Name = "careers.application.sent";

    public override string EventType => Name;
}

/// <summary>Un-ticks the mission when it was the last active application; XP stays (BR-115).</summary>
public sealed record ApplicationWithdrawn(Guid LearnerId, Guid ApplicationId) : IntegrationEvent
{
    public const string Name = "careers.application.withdrawn";

    public override string EventType => Name;
}

/// <summary>Gamification pays +100 on the first one (BR-112, key <c>mission:first-fest-booking</c>).</summary>
public sealed record FestBooked(Guid LearnerId, Guid BookingId, string FestId) : IntegrationEvent
{
    public const string Name = "careers.fest.booked";

    public override string EventType => Name;
}

public sealed record FestCancelled(Guid LearnerId, Guid BookingId) : IntegrationEvent
{
    public const string Name = "careers.fest.cancelled";

    public override string EventType => Name;
}

// ---- Consumed (Careers' own copies; publisher field names) ----

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

public sealed record JobProfileCompleted(Guid LearnerId) : IntegrationEvent
{
    public const string Name = "learner.job-profile.completed";

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
