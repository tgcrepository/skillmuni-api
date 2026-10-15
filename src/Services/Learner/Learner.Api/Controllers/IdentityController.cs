using Asp.Versioning;
using Learner.Application.Contracts;
using Learner.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;

namespace Learner.Api.Controllers;

/// <summary>API-064–067: a government ID linked through DigiLocker (India, A-272).</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/me/identity")]
public sealed class IdentityController(IIdentityLinkService identity) : LearnerControllerBase
{
    /// <summary>The linked ID, or a JSON <c>null</c> when none is linked.</summary>
    [HttpGet]
    [ProducesResponseType<LinkedIdDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Current(CancellationToken cancellationToken) =>
        new JsonResult(await identity.CurrentAsync(CurrentLearnerId, cancellationToken));

    [HttpPost("digilocker/start")]
    public async Task<IActionResult> Start(StartDigiLockerRequest request, CancellationToken cancellationToken) =>
        (await identity.StartAsync(CurrentLearner, request, cancellationToken)).ToActionResult(this);

    [HttpPost("digilocker/link")]
    public async Task<IActionResult> Link(LinkDigiLockerRequest request, CancellationToken cancellationToken) =>
        (await identity.LinkAsync(CurrentLearner, request, cancellationToken)).ToActionResult(this);

    [HttpDelete]
    public async Task<IActionResult> Unlink(CancellationToken cancellationToken) =>
        (await identity.UnlinkAsync(CurrentLearnerId, cancellationToken)).ToActionResult(this);
}
