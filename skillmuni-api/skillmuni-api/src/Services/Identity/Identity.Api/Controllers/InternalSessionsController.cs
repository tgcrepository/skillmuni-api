using Asp.Versioning;
using Identity.Application.Contracts;
using Identity.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace Identity.Api.Controllers;

/// <summary>
/// Gateway → Identity silent refresh. Not routed by the gateway and not in OpenAPI; reachable only inside the
/// Container Apps environment (internal ingress).
/// </summary>
[ApiVersionNeutral]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/sessions")]
[AllowAnonymous]
public sealed class InternalSessionsController(ISessionService sessions, IOptions<AuthOptions> authOptions) : IdentityControllerBase(authOptions)
{
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshSessionRequest request, CancellationToken cancellationToken) =>
        (await sessions.RefreshAsync(request.RefreshToken, cancellationToken)).ToActionResult(this);
}
