using Asp.Versioning;
using Identity.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;

namespace Identity.Api.Controllers;

/// <summary>
/// Lookups for other services. Not routed by the gateway (it proxies only <c>/api/v1/…</c>) and not in OpenAPI;
/// reachable only inside the Container Apps environment (internal ingress). Callers send <c>X-Tenant-Id</c>. Returns
/// an id only, never contact details; personal data travels in the body, never in the URL.
/// </summary>
[ApiController]
[ApiVersionNeutral]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/accounts")]
[AllowAnonymous]
public sealed class InternalAccountsController(IAccountLookupService lookups) : ControllerBase
{
    /// <summary><c>{ email }</c> → <c>{ accountId }</c>; 404 <c>account-not-found</c>, 422 <c>invalid-email</c>.</summary>
    [HttpPost("email-owner")]
    public async Task<IActionResult> EmailOwner(EmailOwnerRequest request, CancellationToken cancellationToken) =>
        (await lookups.FindEmailOwnerAsync(request.Email ?? string.Empty, cancellationToken)).ToActionResult(this);
}
