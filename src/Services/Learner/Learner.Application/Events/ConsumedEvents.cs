using System.Text.Json;
using SkillMuni.Common.Messaging;

namespace Learner.Application.Events;

// Learner's own copies of the events it subscribes to (a service never references another service's code).
// Field names must match the publisher's JSON; unknown fields are ignored, so publishers can add fields.

/// <summary>From Identity: a new learner account.</summary>
public sealed record AccountRegistered(Guid AccountId, string FirstName, string LastName, string? Email, string? Phone, string Market, string Source)
    : IntegrationEvent
{
    public const string Name = "identity.learner.registered";

    public override string EventType => Name;
}

/// <summary>From Identity: <c>Kind</c> is <c>phone</c> or <c>email</c>.</summary>
public sealed record ContactVerified(Guid AccountId, string Kind, string Value) : IntegrationEvent
{
    public const string Name = "identity.contact.verified";

    public override string EventType => Name;
}

/// <summary>From Identity: the learner confirmed an institute profile (claim V4); quests 1–5 count as done.</summary>
public sealed record ClaimConfirmed(Guid ClaimId, Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.claim.confirmed";

    public override string EventType => Name;
}

/// <summary>From Identity: MISS-08 erasure.</summary>
public sealed record AccountErased(Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.learner.erased";

    public override string EventType => Name;
}

/// <summary>
/// From Gamification (expected payload, to agree when Gamification is built): the learner's totals after an award.
/// Totals, not deltas, so a replayed or out-of-order event can't double-count.
/// </summary>
public sealed record XpAwarded(
    Guid LearnerId,
    int TotalXp,
    int Level,
    string Rank,
    int LevelStartXp,
    int NextLevelXp,
    int Badges,
    int StreakDays) : IntegrationEvent
{
    public const string Name = "gamification.xp.awarded";

    public override string EventType => Name;
}

/// <summary>From Assessment (expected payload): a Career Drivers attempt scored (A-306).</summary>
public sealed record AssessmentSubmitted(Guid LearnerId, int Attempt, string ArchetypeName, string ArchetypeSummary) : IntegrationEvent
{
    public const string Name = "assessment.attempt.submitted";

    public override string EventType => Name;
}

/// <summary>From CV: quest slug → answers read from the learner's CV (A-245). The learner's own answers win.</summary>
public sealed record CvParsed(Guid CvId, Guid LearnerId, string? Outcome, IReadOnlyDictionary<string, JsonElement> Prefill) : IntegrationEvent
{
    public const string Name = "cv.parse.completed";

    public override string EventType => Name;
}
