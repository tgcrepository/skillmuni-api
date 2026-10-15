using System.Text.Json;
using SkillMuni.Common.Messaging;

namespace Cv.Application;

/// <summary>
/// A CV was read. <see cref="Prefill"/> is quest slug → answers to merge (Learner keeps anything the learner already
/// typed). It carries personal data by necessity; it goes only to Learner's subscription.
/// </summary>
public sealed record CvParseCompleted(Guid CvId, Guid LearnerId, string? Outcome, IReadOnlyDictionary<string, JsonElement> Prefill) : IntegrationEvent
{
    public const string Name = "cv.parse.completed";

    public override string EventType => Name;
}

/// <summary>The learner deleted a CV; the file and extract are gone.</summary>
public sealed record CvDeleted(Guid CvId, Guid LearnerId) : IntegrationEvent
{
    public const string Name = "cv.document.deleted";

    public override string EventType => Name;
}

/// <summary>From Identity: MISS-08 erasure.</summary>
public sealed record AccountErased(Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.learner.erased";

    public override string EventType => Name;
}
