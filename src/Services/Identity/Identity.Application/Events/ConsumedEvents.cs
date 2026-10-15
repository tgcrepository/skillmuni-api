using SkillMuni.Common.Messaging;

namespace Identity.Application.Events;

// Identity's own copies of the events it subscribes to (a service never references another service's code).
// Field names match the publisher's JSON (docs/EVENTS.md); unknown fields are ignored.

/// <summary>From Learner (A-277): the learner's profile photo changed; Identity shows it as <c>AuthUser.photoUrl</c>.</summary>
public sealed record PhotoChanged(Guid LearnerId, string? PhotoUrl) : IntegrationEvent
{
    public const string Name = "learner.photo.changed";

    public override string EventType => Name;
}
