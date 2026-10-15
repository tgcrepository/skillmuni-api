using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Muni.Application;
using Muni.Application.Services;
using SkillMuni.Common.Security;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Muni.Api.Controllers;

/// <summary>API-110–114: Muni chat. Thin: validate (filter), call the service, map the result.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/muni")]
[Produces("application/json")]
[Authorize(Policy = Policies.Learner)]
public sealed class MuniController(IMuniService muni) : ControllerBase
{
    private LearnerRef CurrentLearner => new(
        User.AccountId() ?? throw new InvalidOperationException("Authorized request without a subject claim."),
        User.FindFirst(SkillMuniClaims.Name)?.Value ?? string.Empty,
        User.FindFirst(SkillMuniClaims.Market)?.Value ?? "IN");

    /// <summary>API-110: greeting, "for you right now" suggestions and topic tiles.</summary>
    [HttpGet("welcome")]
    public async Task<ActionResult<MuniWelcomeDto>> Welcome(CancellationToken cancellationToken) =>
        await muni.WelcomeAsync(CurrentLearner, cancellationToken);

    /// <summary>API-111: <c>{ text, intent? }</c> → <c>{ id, blocks, chips }</c>. The exchange is kept for the history.</summary>
    [HttpPost("messages")]
    [Idempotent]
    public async Task<ActionResult<MuniReplyDto>> Ask(AskRequest request, CancellationToken cancellationToken) =>
        await muni.AskAsync(CurrentLearner, request, cancellationToken);

    /// <summary>API-112: the caller's own email or mobile, unmasked (from Learner). Never cached.</summary>
    [HttpPost("reveal")]
    public async Task<IActionResult> Reveal(RevealRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return (await muni.RevealAsync(CurrentLearner, request.Field!, cancellationToken)).ToActionResult(this);
    }

    /// <summary>API-113: <c>{ helpful: true | false | null }</c>; null clears the rating. 204.</summary>
    [HttpPut("messages/{id}/rating")]
    public async Task<IActionResult> Rate(string id, RateRequest request, CancellationToken cancellationToken) =>
        (await muni.RateAsync(CurrentLearner, id, request.Helpful, cancellationToken)).ToActionResult(this);

    /// <summary>API-114: recent conversations, newest first (an array, as muni.api.ts expects).</summary>
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<MuniHistoryItemDto>>> History(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await muni.HistoryAsync(CurrentLearner, cancellationToken));
    }
}
