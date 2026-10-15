using Asp.Versioning;
using Identity.Application.Contracts;
using Identity.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace Identity.Api.Controllers;

/// <summary>API-023 to API-029: institute claim links (V1–V5).</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/claims/{token}")]
public sealed class ClaimsController(IClaimService claims, IOptions<AuthOptions> authOptions) : IdentityControllerBase(authOptions)
{
    /// <summary>404 unknown link · 410 claimed / expired / deleted. Sends the code on first open.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Invite(string token, CancellationToken cancellationToken) =>
        (await claims.GetInviteAsync(token, User.AccountId(), cancellationToken)).ToActionResult(this);

    [HttpPost("otp")]
    [AllowAnonymous]
    public async Task<IActionResult> Resend(string token, CancellationToken cancellationToken) =>
        (await claims.ResendAsync(token, cancellationToken)).ToActionResult(this);

    /// <summary>Signs the learner in (session cookies).</summary>
    [HttpPost("verify")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify(string token, ClaimCodeRequest request, CancellationToken cancellationToken) =>
        SignedIn(await claims.VerifyAsync(token, request, cancellationToken));

    [HttpPost("consent")]
    [Authorize(Policy = Policies.Learner)]
    public async Task<IActionResult> Consent(string token, ClaimConsentRequest request, CancellationToken cancellationToken) =>
        (await claims.ConsentAsync(token, CurrentAccountId, request, cancellationToken)).ToActionResult(this);

    [HttpPost("sections/{slug}/confirm")]
    [Authorize(Policy = Policies.Learner)]
    public async Task<IActionResult> ConfirmSection(string token, string slug, CancellationToken cancellationToken) =>
        (await claims.ConfirmSectionAsync(token, CurrentAccountId, slug, cancellationToken)).ToActionResult(this);

    [HttpPost("finish")]
    [Authorize(Policy = Policies.Learner)]
    public async Task<IActionResult> Finish(string token, CancellationToken cancellationToken) =>
        (await claims.FinishAsync(token, CurrentAccountId, cancellationToken)).ToActionResult(this);

    /// <summary>V5: deletes the CV and everything read from it.</summary>
    [HttpPost("decline")]
    [AllowAnonymous]
    public async Task<IActionResult> Decline(string token, ClaimDeclineRequest request, CancellationToken cancellationToken) =>
        (await claims.DeclineAsync(token, request, cancellationToken)).ToActionResult(this);
}
