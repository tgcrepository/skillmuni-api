using Asp.Versioning;
using Careers.Application.Contracts;
using Careers.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Careers.Api.Controllers;

/// <summary>API-104–109: My Matches and profile publishing (Vibe Check "Send it"). Actions answer 204.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/me")]
public sealed class MatchesController(IMatchesService matches) : CareersControllerBase
{
    [HttpGet("matches/preview")]
    public async Task<ActionResult<MatchPreviewDto>> Preview(CancellationToken cancellationToken) =>
        await matches.PreviewAsync(LearnerId, cancellationToken);

    [HttpPost("profile/publish")]
    [Idempotent]
    public async Task<IActionResult> Publish(VisibilitySettings settings, CancellationToken cancellationToken)
    {
        await matches.PublishAsync(LearnerId, settings, cancellationToken);
        return NoContent();
    }

    /// <summary>Cards show once the profile is published or an institute claim is confirmed (BR-72).</summary>
    [HttpGet("matches")]
    public async Task<ActionResult<MatchBoardDto>> Board(CancellationToken cancellationToken) =>
        await matches.BoardAsync(LearnerId, cancellationToken);

    [HttpPost("matches/{id}/apply")]
    [Idempotent]
    public async Task<IActionResult> Apply(string id, CancellationToken cancellationToken) =>
        (await matches.ApplyAsync(LearnerId, id, cancellationToken)).ToActionResult(this);

    [HttpPost("matches/{id}/save")]
    public async Task<IActionResult> Save(string id, SaveRequest request, CancellationToken cancellationToken) =>
        (await matches.SaveAsync(LearnerId, id, request.Saved, cancellationToken)).ToActionResult(this);

    [HttpPost("matches/{id}/accept")]
    [Idempotent]
    public async Task<IActionResult> Accept(string id, CancellationToken cancellationToken) =>
        (await matches.AcceptAsync(LearnerId, id, cancellationToken)).ToActionResult(this);
}
