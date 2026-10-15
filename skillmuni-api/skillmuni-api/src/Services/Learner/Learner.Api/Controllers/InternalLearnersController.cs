using Asp.Versioning;
using Learner.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;

namespace Learner.Api.Controllers;

/// <summary>
/// Service-to-service reads (CV, Careers, Identity's claim result). Not routed by the gateway and not in OpenAPI;
/// reachable only inside the Container Apps environment (internal ingress). Callers send <c>X-Tenant-Id</c>.
/// </summary>
[ApiController]
[ApiVersionNeutral]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/learners")]
[AllowAnonymous]
public sealed class InternalLearnersController(IProfileProgressService profileProgress) : ControllerBase
{
    /// <summary>A-262 completeness, quest by quest.</summary>
    [HttpGet("{learnerId:guid}/profile-progress")]
    public async Task<IActionResult> ProfileProgress(Guid learnerId, CancellationToken cancellationToken) =>
        (await profileProgress.GetAsync(learnerId, cancellationToken)).ToActionResult(this);
}
