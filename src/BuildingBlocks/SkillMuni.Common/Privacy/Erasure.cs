using SkillMuni.Common.Results;

namespace SkillMuni.Common.Privacy;

/// <summary>
/// MISS-08 erasure tombstones: the ids of learners who erased their account. A service that builds a learner's rows
/// on first use keeps one, so a still-valid access token (up to 10 minutes after erasure) or an event delivered late
/// can't bring the learner back as an empty profile.
/// </summary>
public interface IErasureTombstones
{
    /// <summary>True when this learner erased their account (tombstone saved, or added in the current unit of work).</summary>
    Task<bool> IsErasedAsync(Guid learnerId, CancellationToken cancellationToken);

    /// <summary>Adds the tombstone to the current unit of work (saved with the handler's changes). Safe to call twice.</summary>
    Task MarkErasedAsync(Guid learnerId, CancellationToken cancellationToken);
}

public static class ErasureErrors
{
    /// <summary>410: any request from an erased account (the frontend signs the learner out).</summary>
    public static readonly Error LearnerErased = Error.Gone("learner-erased", "This account has been deleted");
}
