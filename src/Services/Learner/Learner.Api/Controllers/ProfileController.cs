using Asp.Versioning;
using Learner.Application.Contracts;
using Learner.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Learner.Api.Controllers;

/// <summary>API-115–117, 120, 121: Profile v2 · Player ID. Contact changes (API-118/119) are Identity's.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/profile")]
public sealed class ProfileController(IProfileService profile) : LearnerControllerBase
{
    private const long MaxPhotoUpload = 6 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<PlayerProfileDto>> Get(CancellationToken cancellationToken) =>
        await profile.GetAsync(CurrentLearner, cancellationToken);

    [HttpPost("reveal")]
    public async Task<ActionResult<RevealDto>> Reveal(RevealRequest request, CancellationToken cancellationToken)
    {
        // Revealed values are personal: never cache them anywhere.
        Response.Headers.CacheControl = "no-store";
        return await profile.RevealAsync(CurrentLearner, request.Field, cancellationToken);
    }

    [HttpGet("edit")]
    public async Task<ActionResult<ProfileEditDto>> Editable(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return await profile.GetEditableAsync(CurrentLearner, cancellationToken);
    }

    [HttpPut]
    public async Task<IActionResult> Save(ProfileEditRequest request, CancellationToken cancellationToken) =>
        (await profile.SaveAsync(CurrentLearner, request, cancellationToken)).ToActionResult(this);

    /// <summary>Multipart field <c>photo</c>: JPG, PNG, WEBP or HEIC up to 5 MB.</summary>
    [HttpPost("photo")]
    [Idempotent]
    [RequestSizeLimit(MaxPhotoUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxPhotoUpload)]
    public async Task<IActionResult> UploadPhoto(IFormFile? photo, CancellationToken cancellationToken) =>
        (await profile.UploadPhotoAsync(CurrentLearner, ToUpload(photo), cancellationToken)).ToActionResult(this);

    /// <summary>The learner's own photo. <c>?v=</c> changes with every upload, so it can be cached privately.</summary>
    [HttpGet("photo")]
    [Produces("image/jpeg", "image/png", "image/webp", "image/heic")]
    public async Task<IActionResult> Photo(CancellationToken cancellationToken)
    {
        var result = await profile.GetPhotoAsync(CurrentLearnerId, cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiProblems.Result(HttpContext, result.Error);
        }

        Response.Headers.CacheControl = "private, max-age=86400";
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        return File(result.Value.Content, result.Value.ContentType);
    }
}
