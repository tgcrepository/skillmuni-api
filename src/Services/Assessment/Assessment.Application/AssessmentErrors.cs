using SkillMuni.Common.Results;

namespace Assessment.Application;

public static class AssessmentErrors
{
    /// <summary>BR-54: submit needs all 36 pairs answered (the mock's 422 <c>{ code: 'incomplete' }</c>).</summary>
    public static readonly Error Incomplete = Error.Validation("incomplete", "Answer every pair before opening your jars");

    /// <summary>No submitted attempt yet (the mock's 404).</summary>
    public static readonly Error NoResult = Error.NotFound("result-not-found", "Take the assessment to see your results");
}
