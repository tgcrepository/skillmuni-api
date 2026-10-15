using Asp.Versioning;
using Learner.Application.Contracts;
using Learner.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Learner.Api.Controllers;

/// <summary>API-058–060: Quest 4's trophy shelf.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/me/trophies")]
public sealed class TrophiesController(ITrophyService trophies) : LearnerControllerBase
{
    private const long MaxUpload = 11 * 1024 * 1024;

    /// <summary>{ verified } within 5 s, or 504 link-timeout. Never blocks saving the trophy.</summary>
    [HttpPost("check-link")]
    public async Task<IActionResult> CheckLink(CheckLinkRequest request, CancellationToken cancellationToken) =>
        (await trophies.CheckLinkAsync(CurrentLearner, request, cancellationToken)).ToActionResult(this);

    /// <summary>Multipart field <c>file</c>: PDF, JPG, PNG, WEBP or HEIC up to 10 MB.</summary>
    [HttpPost("files")]
    [Idempotent]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken) =>
        (await trophies.UploadAsync(CurrentLearner, ToUpload(file), cancellationToken)).ToActionResult(this);

    [HttpPost("digilocker")]
    [Idempotent]
    public async Task<IActionResult> ImportDigiLocker(CancellationToken cancellationToken) =>
        (await trophies.ImportDigiLockerAsync(CurrentLearner, cancellationToken)).ToActionResult(this);
}
