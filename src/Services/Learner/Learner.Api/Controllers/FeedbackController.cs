using Asp.Versioning;
using Learner.Application.Contracts;
using Learner.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Idempotency;

namespace Learner.Api.Controllers;

/// <summary>API-040 / A-274.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/me/feedback")]
public sealed class FeedbackController(IFeedbackService feedback) : LearnerControllerBase
{
    /// <summary>The XP earned: 5 at most once every 30 days, otherwise 0.</summary>
    [HttpPost]
    [Idempotent]
    public async Task<ActionResult<FeedbackResultDto>> Send(FeedbackRequest request, CancellationToken cancellationToken) =>
        await feedback.SendAsync(CurrentLearner, request, cancellationToken);
}
