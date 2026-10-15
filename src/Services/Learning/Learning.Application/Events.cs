using SkillMuni.Common.Messaging;

namespace Learning.Application;

// Event field names follow docs/EVENTS.md. Learning's terms map onto the catalogue's as follows:
//   courseSlug = the app's module, /learn/:module (e.g. "brain-vita");
//   moduleSlug = the app's topic inside it (e.g. "non-verbal-reasoning"). Topic slugs repeat across courses, so
//   (courseSlug, moduleSlug) is the unique key. This matches BRD §4.3 ("Brain-Vita Boss: all Brain-Vita modules").

// ---- Published ----

/// <summary>A brief's quiz checked. Only <c>FirstAttempt</c> pays XP (+1 per correct, BR-106) and keeps the streak (B-45).</summary>
public sealed record QuizSubmitted(
    Guid LearnerId,
    string QuizId,
    string BriefId,
    string ModuleSlug,
    string CourseSlug,
    int Correct,
    int Total,
    bool FirstAttempt) : IntegrationEvent, ILearnerEvent
{
    public const string Name = "learning.quiz.submitted";

    public override string EventType => Name;
}

/// <summary>
/// A brief completed: its quiz was submitted for the first time (the app marks the brief done then, whatever the
/// score). Sent once per learner and brief, so <c>FirstCompletion</c> is always true; the field stays for the contract.
/// </summary>
public sealed record BriefCompleted(Guid LearnerId, string BriefId, string ModuleSlug, string CourseSlug, bool FirstCompletion)
    : IntegrationEvent, ILearnerEvent
{
    public const string Name = "learning.brief.completed";

    public override string EventType => Name;
}

/// <summary>Every brief of a topic (event "module") completed. Sent once.</summary>
public sealed record ModuleCompleted(Guid LearnerId, string ModuleSlug, string CourseSlug) : IntegrationEvent, ILearnerEvent
{
    public const string Name = "learning.module.completed";

    public override string EventType => Name;
}

/// <summary>Every brief of every topic of an app module (event "course") completed. Sent once.</summary>
public sealed record CourseCompleted(Guid LearnerId, string CourseSlug) : IntegrationEvent, ILearnerEvent
{
    public const string Name = "learning.course.completed";

    public override string EventType => Name;
}

/// <summary>MISS-04: minutes spent learning, as the app reports them (after the daily cap).</summary>
public sealed record ActivityRecorded(Guid LearnerId, int Minutes) : IntegrationEvent, ILearnerEvent
{
    public const string Name = "learning.activity.recorded";

    public override string EventType => Name;
}

/// <summary>BR-107: the Lobby first-moves bonus collected. Gamification credits +50 once (key <c>first-moves:once</c>).</summary>
public sealed record FirstMovesClaimed(Guid LearnerId) : IntegrationEvent, ILearnerEvent
{
    public const string Name = "learning.first-moves.claimed";

    public override string EventType => Name;
}

// ---- Consumed (Learning's own copies; unknown publisher fields are ignored) ----

/// <summary>From Identity: MISS-08 erasure.</summary>
public sealed record AccountErased(Guid AccountId) : IntegrationEvent
{
    public const string Name = "identity.learner.erased";

    public override string EventType => Name;
}

/// <summary>From Assessment: Career Drivers submitted; the first move "Discover your Career Drivers" is done (BR-42).</summary>
public sealed record AssessmentSubmitted(Guid LearnerId) : IntegrationEvent, ILearnerEvent
{
    public const string Name = "assessment.attempt.submitted";

    public override string EventType => Name;
}
