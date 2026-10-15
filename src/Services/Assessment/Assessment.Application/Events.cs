using SkillMuni.Common.Messaging;

namespace Assessment.Application;

// ---- Published (docs/EVENTS.md) ----

/// <summary>
/// Islands 1–3: the island's 9th pair was answered (BR-104). Once per island per attempt. Gamification pays +25 only
/// when <see cref="FirstAttempt"/>, key <c>assessment:island:{n}</c>.
/// </summary>
public sealed record IslandCleared(Guid LearnerId, int Attempt, int Island, bool FirstAttempt) : IntegrationEvent, ILearnerEvent
{
    public const string Name = "assessment.island.cleared";

    public override string EventType => Name;
}

/// <summary>
/// "Open my jars": an attempt was scored (BR-105). <see cref="TopDrivers"/> are the top 3 driver names, highest first
/// (the Launchpad and Profile "traits"); <see cref="TopDriverIds"/> the same as ids A–I.
/// </summary>
public sealed record AttemptSubmitted(
    Guid LearnerId,
    int Attempt,
    bool FirstAttempt,
    string ArchetypeId,
    string ArchetypeName,
    string ArchetypeEmoji,
    string ArchetypeSummary,
    IReadOnlyList<string> TopDrivers,
    IReadOnlyList<string> TopDriverIds) : IntegrationEvent, ILearnerEvent
{
    public const string Name = "assessment.attempt.submitted";

    public override string EventType => Name;
}

// ---- Consumed (Assessment's own copies; publisher field names) ----

public sealed record AccountRegistered(Guid AccountId, string FirstName, string LastName, string Market) : IntegrationEvent, ILearnerEvent
{
    public const string Name = "identity.learner.registered";

    public override string EventType => Name;

    Guid ILearnerEvent.LearnerId => AccountId;
}

public sealed record AccountErased(Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.learner.erased";

    public override string EventType => Name;
}
