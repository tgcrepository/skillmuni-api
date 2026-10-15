using Asp.Versioning;
using Learning.Application.Contracts;
using Learning.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Learning.Api.Controllers;

/// <summary>API-061…063: the Lobby (home.api.ts).</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/home")]
public sealed class HomeController(IHomeService home) : LearningControllerBase
{
    /// <summary>API-063: everything the Lobby needs in one call.</summary>
    [HttpGet]
    public async Task<ActionResult<HomeFeedDto>> Feed(CancellationToken cancellationToken) =>
        await home.FeedAsync(LearnerId, cancellationToken);

    /// <summary>API-061: a first move done by visiting it (<c>jobs</c>); 204.</summary>
    [HttpPost("first-steps/{id}")]
    public async Task<IActionResult> CompleteStep(string id, CancellationToken cancellationToken) =>
        (await home.CompleteStepAsync(LearnerId, id, cancellationToken)).ToActionResult(this);

    /// <summary>API-062: +50 once all 3 first moves are done (BR-107); 409 <c>first-moves-not-done</c> before; 0 after.</summary>
    [HttpPost("first-moves/claim")]
    [Idempotent]
    [ProducesResponseType<FirstMovesClaimDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ClaimFirstMoves(CancellationToken cancellationToken) =>
        (await home.ClaimFirstMovesAsync(LearnerId, cancellationToken)).ToActionResult(this);
}
