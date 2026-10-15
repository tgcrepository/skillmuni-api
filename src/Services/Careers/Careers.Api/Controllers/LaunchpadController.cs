using Asp.Versioning;
using Careers.Application.Contracts;
using Careers.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;

namespace Careers.Api.Controllers;

/// <summary>API-090–096: Career Launchpad. One endpoint per card, so one failing source never blocks the page.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/launchpad")]
public sealed class LaunchpadController(ILaunchpadService launchpad) : CareersControllerBase
{
    [HttpGet("readiness")]
    public async Task<ActionResult<ReadinessDto>> Readiness(CancellationToken cancellationToken) =>
        await launchpad.ReadinessAsync(LearnerId, cancellationToken);

    /// <summary>503 <c>learner-unavailable</c> when Learner can't be reached.</summary>
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken) =>
        (await launchpad.ProfileAsync(LearnerId, cancellationToken)).ToActionResult(this);

    [HttpGet("assessment")]
    public async Task<ActionResult<AssessmentCardDto>> Assessment(CancellationToken cancellationToken) =>
        await launchpad.AssessmentAsync(LearnerId, cancellationToken);

    [HttpGet("jobs")]
    public async Task<ActionResult<JobsCardDto>> Jobs(CancellationToken cancellationToken) =>
        await launchpad.JobsAsync(LearnerId, cancellationToken);

    [HttpGet("fest")]
    public async Task<ActionResult<FestCardDto>> Fest(CancellationToken cancellationToken) =>
        await launchpad.FestAsync(LearnerId, cancellationToken);

    [HttpGet("sectors")]
    public async Task<ActionResult<IReadOnlyList<SectorDto>>> Sectors(CancellationToken cancellationToken) =>
        Ok(await launchpad.SectorsAsync(LearnerId, cancellationToken));

    /// <summary>Turns Hire Fest alerts (city and online) on or off together.</summary>
    [HttpPut("fest/alerts")]
    public async Task<ActionResult<FestCardDto>> SetFestAlerts(FestAlertsToggle request, CancellationToken cancellationToken) =>
        await launchpad.SetFestAlertsAsync(LearnerId, request.On, cancellationToken);
}
