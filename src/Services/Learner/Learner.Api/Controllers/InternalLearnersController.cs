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
public sealed class InternalLearnersController(IProfileProgressService profileProgress, ICareersProfileService careersProfile) : ControllerBase
{
    /// <summary>A-262 completeness, quest by quest.</summary>
    [HttpGet("{learnerId:guid}/profile-progress")]
    public async Task<IActionResult> ProfileProgress(Guid learnerId, CancellationToken cancellationToken) =>
        (await profileProgress.GetAsync(learnerId, cancellationToken)).ToActionResult(this);

    /// <summary>For Careers' matching: name, masked mobile, city/state, relocate and the skill deck with evidence; 404 without a learner.</summary>
    [HttpGet("{learnerId:guid}/careers-profile")]
    public async Task<IActionResult> CareersProfile(Guid learnerId, CancellationToken cancellationToken) =>
        (await careersProfile.GetAsync(learnerId, cancellationToken)).ToActionResult(this);
}
