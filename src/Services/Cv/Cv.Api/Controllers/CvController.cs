using Asp.Versioning;
using Cv.Application;
using Cv.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Common.Security;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace Cv.Api.Controllers;

/// <summary>API-036–039: CV upload, parse status, what was found, delete. Parse outcomes travel in the status.</summary>
[ApiController]
[ApiVersion(1.0)]
[Produces("application/json")]
[Route("api/v{version:apiVersion}/cv")]
[Authorize(Policy = Policies.Learner)]
public sealed class CvController(ICvService cvs) : ControllerBase
{
    private const long MaxUpload = 11 * 1024 * 1024;

    private Guid LearnerId => User.AccountId() ?? throw new InvalidOperationException("Authorized request without a subject claim.");

    private string Market => User.FindFirst(SkillMuniClaims.Market)?.Value ?? "IN";

    /// <summary>Multipart field <c>file</c>, or several <c>pages</c> (photos of a paper CV). 201 with <c>{ cvId }</c>.</summary>
    [HttpPost]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<IActionResult> Upload(IFormFile? file, [FromForm] List<IFormFile>? pages, CancellationToken cancellationToken)
    {
        var uploads = (file is null ? pages ?? [] : [file])
            .Select(f => new UploadedFile(f.FileName, f.ContentType, f.Length, f.OpenReadStream()))
            .ToList();
        try
        {
            return (await cvs.UploadAsync(LearnerId, Market, uploads, cancellationToken))
                .ToActionResult(this, created => Created($"/api/v1/cv/{created.CvId}/status", created));
        }
        finally
        {
            foreach (var upload in uploads)
            {
                await upload.Content.DisposeAsync();
            }
        }
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, CancellationToken cancellationToken)
    {
        // Polled every second while parsing; never cached.
        Response.Headers.CacheControl = "no-store";
        return (await cvs.StatusAsync(LearnerId, id, cancellationToken)).ToActionResult(this);
    }

    /// <summary>409 cv-not-ready while parsing; 410 cv-parse-failed after a failed parse.</summary>
    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> Summary(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return (await cvs.SummaryAsync(LearnerId, id, cancellationToken)).ToActionResult(this);
    }

    /// <summary>Deletes the file and everything read from it.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        (await cvs.DeleteAsync(LearnerId, id, cancellationToken)).ToActionResult(this);
}
