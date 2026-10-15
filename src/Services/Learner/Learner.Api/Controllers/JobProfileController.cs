using Asp.Versioning;
using Learner.Application.Contracts;
using Learner.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Learner.Api.Controllers;

/// <summary>API-068…074: My Job Profile, built from the 8 quests (A-262).</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/job-profile")]
public sealed class JobProfileController(IJobProfileService jobProfile) : LearnerControllerBase
{
    private const long MaxUpload = 6 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<JobProfileDto>> Get(CancellationToken cancellationToken) =>
        await jobProfile.GetAsync(CurrentLearner, cancellationToken);

    /// <summary>Full contact for the learner's own CV file only.</summary>
    [HttpGet("cv-contact")]
    public async Task<ActionResult<CvContactDto>> CvContact(CancellationToken cancellationToken) =>
        await jobProfile.GetCvContactAsync(CurrentLearner, cancellationToken);

    /// <summary>FR-13: up to 400 characters; empty text goes back to Muni's summary.</summary>
    [HttpPut("about")]
    public async Task<ActionResult<JobProfileDto>> SaveAbout(SaveAboutRequest request, CancellationToken cancellationToken) =>
        await jobProfile.SaveAboutAsync(CurrentLearner, request, cancellationToken);

    /// <summary>FR-14: multipart field <c>cv</c>, PDF or Word up to 5 MB → the suggested changes (nothing applied yet).</summary>
    [HttpPost("cv")]
    [Idempotent]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<IActionResult> UploadCv(IFormFile? cv, CancellationToken cancellationToken) =>
        (await jobProfile.UploadCvAsync(CurrentLearner, ToUpload(cv), cancellationToken)).ToActionResult(this);

    /// <summary>Adds the accepted changes to the quests; contact changes are never applied (A-207).</summary>
    [HttpPost("cv/apply")]
    [Idempotent]
    public async Task<ActionResult<JobProfileDto>> ApplyChanges(ApplyCvChangesRequest request, CancellationToken cancellationToken) =>
        await jobProfile.ApplyChangesAsync(CurrentLearner, request, cancellationToken);

    [HttpGet("share")]
    public async Task<ActionResult<ShareSettingsDto>> Share(CancellationToken cancellationToken) =>
        await jobProfile.GetShareAsync(CurrentLearner, cancellationToken);

    /// <summary>A-208: <c>expiresDays</c> is 7, 30 or 90.</summary>
    [HttpPut("share")]
    public async Task<ActionResult<ShareSettingsDto>> UpdateShare(UpdateShareRequest request, CancellationToken cancellationToken) =>
        await jobProfile.UpdateShareAsync(CurrentLearner, request, cancellationToken);
}
