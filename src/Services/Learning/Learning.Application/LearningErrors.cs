using SkillMuni.Common.Results;

namespace Learning.Application;

/// <summary>Every expected failure, with the business code the frontend reads from <c>errors.code[0]</c>.</summary>
public static class LearningErrors
{
    public static readonly Error ModuleNotFound = Error.NotFound("module-not-found", "We couldn't find that module");

    public static readonly Error TopicNotFound = Error.NotFound("topic-not-found", "We couldn't find that topic");

    public static readonly Error BriefNotFound = Error.NotFound("brief-not-found", "We couldn't find that brief");

    public static readonly Error QuizNotFound = Error.NotFound("quiz-not-found", "We couldn't find that quiz");

    public static readonly Error QuizLocked = Error.Validation("quiz-locked", "Finish the briefs before this one first");

    public static readonly Error StepNotFound = Error.NotFound("step-not-found", "We couldn't find that first move");

    public static readonly Error FirstMovesNotDone = Error.Conflict("first-moves-not-done", "Finish all three first moves to collect the bonus");
}
