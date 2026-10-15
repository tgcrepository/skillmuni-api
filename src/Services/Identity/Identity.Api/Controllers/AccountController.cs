using Asp.Versioning;
using Identity.Application.Contracts;
using Identity.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Paging;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Identity.Api.Controllers;

/// <summary>MISS-07 consents, MISS-08 privacy rights, MISS-13 device sessions.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/me")]
[Authorize(Policy = Policies.Learner)]
public sealed class AccountController(
    IConsentService consents,
    ISessionService sessions,
    IPrivacyService privacy,
    IOptions<AuthOptions> authOptions) : IdentityControllerBase(authOptions)
{
    [HttpGet("consents")]
    public async Task<IActionResult> Consents([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        Ok(await consents.ListAsync(CurrentAccountId, PageRequest.Create(page, pageSize), cancellationToken));

    [HttpPost("consents")]
    [Idempotent]
    public async Task<IActionResult> RecordConsent(RecordConsentRequest request, CancellationToken cancellationToken) =>
        (await consents.RecordAsync(CurrentAccountId, request, cancellationToken))
            .ToActionResult(this, consent => Created($"/api/v1/me/consents?scope={Uri.EscapeDataString(consent.Scope)}", consent));

    [HttpDelete("consents/{scope}")]
    public async Task<IActionResult> WithdrawConsent(string scope, CancellationToken cancellationToken) =>
        (await consents.WithdrawAsync(CurrentAccountId, scope, cancellationToken)).ToActionResult(this);

    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        Ok(await sessions.ListAsync(CurrentAccountId, User.SessionId(), PageRequest.Create(page, pageSize), cancellationToken));

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken cancellationToken) =>
        (await sessions.RevokeAsync(CurrentAccountId, id, cancellationToken)).ToActionResult(this);

    /// <summary>202: every service assembles its part; the learner is notified when the export is ready.</summary>
    [HttpPost("data-export")]
    [Idempotent]
    public async Task<IActionResult> RequestExport(CancellationToken cancellationToken) =>
        (await privacy.RequestExportAsync(CurrentAccountId, cancellationToken)).ToActionResult(this, accepted => Accepted(accepted));

    /// <summary>Erasure: personal details removed here and, by event, in every other service.</summary>
    [HttpDelete]
    public async Task<IActionResult> Erase(CancellationToken cancellationToken)
    {
        var result = await privacy.EraseAsync(CurrentAccountId, cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiProblems.Result(HttpContext, result.Error);
        }

        ClearCookies();
        return NoContent();
    }
}
