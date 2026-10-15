using System.Globalization;
using Asp.Versioning;
using Assessment.Application;
using Assessment.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Assessment.Api.Controllers;

/// <summary>API-004–010: Career Drivers ("Muni's Balance").</summary>
[ApiVersion(1.0)]
public sealed class CareerAssessmentController(ICareerAssessmentService assessment) : AssessmentControllerBase
{
    /// <summary>API-004: the pairs, islands and the learner's attempt in progress.</summary>
    [HttpGet("assessments/career")]
    public async Task<ActionResult<AssessmentStateDto>> State(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return await assessment.StateAsync(LearnerId, cancellationToken);
    }

    /// <summary>API-005: auto-save after every pick. <c>xpEarned</c> &gt; 0 when the save cleared island 1, 2 or 3.</summary>
    [HttpPut("assessments/career/answers")]
    public async Task<ActionResult<SaveAnswersResponse>> Save(SaveAnswersRequest request, CancellationToken cancellationToken) =>
        await assessment.SaveAsync(LearnerId, request, cancellationToken);

    /// <summary>API-006: "Open my jars". 422 <c>incomplete</c> unless all 36 pairs are answered (BR-54).</summary>
    [HttpPost("assessments/career/submit")]
    [Idempotent]
    public async Task<IActionResult> Submit(SubmitAnswersRequest request, CancellationToken cancellationToken) =>
        (await assessment.SubmitAsync(LearnerId, request, cancellationToken)).ToActionResult(this);

    /// <summary>API-007: <c>?attempt=n</c>, or the latest. 404 <c>result-not-found</c> before the first submit.</summary>
    [HttpGet("assessments/career/result")]
    public async Task<IActionResult> Result([FromQuery] string? attempt, CancellationToken cancellationToken)
    {
        // Like the app's mock: anything that isn't a positive number means "the latest".
        int? number = int.TryParse(attempt, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : null;
        return (await assessment.ResultAsync(LearnerId, number, cancellationToken)).ToActionResult(this);
    }

    /// <summary>API-008: start this attempt again from pair 1; islands already cleared don't pay again.</summary>
    [HttpPost("assessments/career/restart")]
    [Idempotent]
    public async Task<ActionResult<AssessmentStateDto>> Restart(CancellationToken cancellationToken) =>
        await assessment.RestartAsync(LearnerId, cancellationToken);

    /// <summary>API-009: a new attempt after a submitted one; retakes earn no XP.</summary>
    [HttpPost("assessments/career/retake")]
    [Idempotent]
    public async Task<ActionResult<AssessmentStateDto>> Retake(CancellationToken cancellationToken) =>
        await assessment.RetakeAsync(LearnerId, cancellationToken);

    /// <summary>API-010: "Remind me tomorrow" (one nudge at 6 PM).</summary>
    [HttpPut("assessments/career/remind")]
    public async Task<ActionResult<RemindResponse>> Remind(RemindRequest request, CancellationToken cancellationToken) =>
        await assessment.SetRemindAsync(LearnerId, request.Remind ?? false, cancellationToken);
}
