using Asp.Versioning;
using Identity.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;

namespace Identity.Api.Controllers;

/// <summary>
/// Lookups for other services. Not routed by the gateway and not in OpenAPI; reachable only inside the Container
/// Apps environment (internal ingress). Callers send <c>X-Tenant-Id</c>. Returns an id only, never contact details.
/// </summary>
[ApiController]
[ApiVersionNeutral]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/accounts")]
[AllowAnonymous]
public sealed class InternalAccountsController(IAccountLookupService lookups) : ControllerBase
{
    [HttpGet("email-owner")]
    public async Task<IActionResult> EmailOwner([FromQuery] string email, CancellationToken cancellationToken) =>
        (await lookups.FindEmailOwnerAsync(email, cancellationToken)).ToActionResult(this);
}
