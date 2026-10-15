using SkillMuni.Common.Messaging;

namespace Learner.Application.Events;

// Events Learner publishes. Gamification credits XP and badges from them (once per reason + source id);
// payloads carry ids, never answers or contact details.

/// <summary>A quest step done for the first time, in the quest in progress (A-249 step XP).</summary>
public sealed record QuestStepCompleted(Guid LearnerId, string Slug, int Quest, int Part) : IntegrationEvent
{
    public const string Name = "learner.quest.step-completed";

    public override string EventType => Name;
}

/// <summary>A quest finished for the first time (quest XP and its badge).</summary>
public sealed record QuestCompleted(Guid LearnerId, string Slug, int Quest, string Badge) : IntegrationEvent
{
    public const string Name = "learner.quest.completed";

    public override string EventType => Name;
}

/// <summary>A profile-strength task done for the first time: <c>photo</c> or <c>skills</c>.</summary>
public sealed record ProfileTaskCompleted(Guid LearnerId, string Task) : IntegrationEvent
{
    public const string Name = "learner.profile.task-completed";

    public override string EventType => Name;
}

/// <summary>Programme code bonus quest submitted (Figma section 13). Payments get the stipend set-up from Learner's API.</summary>
public sealed record ProgrammeEnrolled(Guid LearnerId, string ProgrammeCode, bool PaysStipend) : IntegrationEvent
{
    public const string Name = "learner.programme.enrolled";

    public override string EventType => Name;
}

/// <summary>A-274. <see cref="EarnsXp"/> is true at most once per 30 days.</summary>
public sealed record FeedbackSent(Guid LearnerId, int Mood, bool EarnsXp) : IntegrationEvent
{
    public const string Name = "learner.feedback.sent";

    public override string EventType => Name;
}

/// <summary>A-277: Identity copies the URL into <c>AuthUser.photoUrl</c> for the top bar.</summary>
public sealed record PhotoChanged(Guid LearnerId, string PhotoUrl) : IntegrationEvent
{
    public const string Name = "learner.photo.changed";

    public override string EventType => Name;
}

/// <summary>A-272: a government ID linked or removed (no number in the event).</summary>
public sealed record IdentityLinkChanged(Guid LearnerId, string Document, bool Linked) : IntegrationEvent
{
    public const string Name = "learner.identity.link-changed";

    public override string EventType => Name;
}
