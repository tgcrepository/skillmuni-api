using Asp.Versioning;
using Learner.Application.Contracts;
using Learner.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;

namespace Learner.Api.Controllers;

/// <summary>API-125–128: progress and the 8 registration quests.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/me")]
public sealed class QuestsController(IQuestService quests, IProgressService progress) : LearnerControllerBase
{
    [HttpGet("progress")]
    public async Task<ActionResult<LearnerProgressDto>> Progress(CancellationToken cancellationToken) =>
        await progress.GetAsync(CurrentLearner, cancellationToken);

    [HttpGet("quests/{slug}/answers")]
    public async Task<IActionResult> Answers(string slug, CancellationToken cancellationToken) =>
        (await quests.GetAnswersAsync(CurrentLearner, slug, cancellationToken)).ToActionResult(this);

    /// <summary>204. Idempotent per part (the offline queue may replay it).</summary>
    [HttpPut("quests/{slug}/parts/{part:int}")]
    public async Task<IActionResult> SavePart(string slug, int part, SavePartRequest request, CancellationToken cancellationToken) =>
        (await quests.SavePartAsync(CurrentLearner, slug, part, request, cancellationToken)).ToActionResult(this);

    /// <summary>xpEarned is 0 when the quest was already complete.</summary>
    [HttpPost("quests/{slug}/complete")]
    public async Task<IActionResult> Complete(string slug, CancellationToken cancellationToken) =>
        (await quests.CompleteAsync(CurrentLearner, slug, cancellationToken)).ToActionResult(this);
}
