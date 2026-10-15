namespace SkillMuni.Common.Messaging;

/// <summary>
/// An event about one learner. When the consuming service keeps erasure tombstones
/// (<see cref="SkillMuni.Common.Privacy.IErasureTombstones"/>), an event for an erased learner is recorded in the inbox and skipped.
/// Events whose field is <c>accountId</c> implement <see cref="LearnerId"/> explicitly, so the JSON is unchanged.
/// </summary>
public interface ILearnerEvent
{
    Guid LearnerId { get; }
}
