using Asp.Versioning;
using Careers.Application;
using Careers.Application.Contracts;
using Careers.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Careers.Api.Controllers;

/// <summary>
/// API-075–089: My Jobs. Lists take jobs.api.ts <c>ListQuery</c> as <c>?q&amp;place&amp;sort&amp;limit&amp;filters</c> (filters is JSON)
/// and return its <c>ListResult</c> (<c>items, total, facets, suggestions, note</c>), not the generic paging shape.
/// </summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/jobs")]
public sealed class JobsController(IJobsService jobs, IOptions<CareersOptions> options) : CareersControllerBase
{
    /// <summary>FR-42: CV files up to 5 MB (plus multipart overhead).</summary>
    private const long MaxUpload = 6 * 1024 * 1024;

    /// <summary>Also clears the Launchpad's "new matches" (BR-62).</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<JobsSummaryDto>> Summary(CancellationToken cancellationToken) =>
        await jobs.SummaryAsync(LearnerId, cancellationToken);

    /// <summary><c>tab</c>: available · matching (match ≥ 60 %) · bookmarked.</summary>
    [HttpGet("list/{tab}")]
    public async Task<IActionResult> List(
        string tab,
        [FromQuery] string? q,
        [FromQuery] string? place,
        [FromQuery] string? sort,
        [FromQuery] int? limit,
        [FromQuery] string? filters,
        CancellationToken cancellationToken) =>
        (await jobs.JobsAsync(LearnerId, tab, Query(q, place, sort, limit, filters), cancellationToken)).ToActionResult(this);

    [HttpGet("invitations")]
    public async Task<ActionResult<ListResultDto<InvitationDto>>> Invitations(
        [FromQuery] string? q,
        [FromQuery] string? place,
        [FromQuery] string? sort,
        [FromQuery] int? limit,
        [FromQuery] string? filters,
        CancellationToken cancellationToken) =>
        await jobs.InvitationsAsync(LearnerId, Query(q, place, sort, limit, filters), cancellationToken);

    [HttpGet("applications")]
    public async Task<ActionResult<ListResultDto<ApplicationDto>>> Applications(
        [FromQuery] string? q,
        [FromQuery] string? place,
        [FromQuery] string? sort,
        [FromQuery] int? limit,
        [FromQuery] string? filters,
        CancellationToken cancellationToken) =>
        await jobs.ApplicationsAsync(LearnerId, Query(q, place, sort, limit, filters), cancellationToken);

    [HttpGet("job/{id}")]
    public async Task<IActionResult> Job(string id, CancellationToken cancellationToken) =>
        (await jobs.JobAsync(LearnerId, id, cancellationToken)).ToActionResult(this);

    [HttpGet("invitations/{id}")]
    public async Task<IActionResult> Invitation(string id, CancellationToken cancellationToken) =>
        (await jobs.InvitationAsync(LearnerId, id, cancellationToken)).ToActionResult(this);

    [HttpGet("applications/{id}")]
    public async Task<IActionResult> Application(string id, CancellationToken cancellationToken) =>
        (await jobs.ApplicationAsync(LearnerId, id, cancellationToken)).ToActionResult(this);

    [HttpPut("job/{id}/saved")]
    public async Task<IActionResult> Save(string id, SaveRequest request, CancellationToken cancellationToken) =>
        (await jobs.SaveAsync(LearnerId, id, request.Saved, cancellationToken)).ToActionResult(this);

    [HttpGet("job/{id}/apply")]
    public async Task<IActionResult> ApplyInfo(string id, CancellationToken cancellationToken) =>
        (await jobs.ApplyInfoAsync(LearnerId, id, DisplayName, cancellationToken)).ToActionResult(this);

    /// <summary>Multipart field <c>file</c> (PDF, .docx or .doc, ≤ 5 MB), kept in Blob Storage. Returns <c>{ id, name, meta }</c>.</summary>
    [HttpPost("cv")]
    [Idempotent]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<IActionResult> UploadCv(IFormFile? file, CancellationToken cancellationToken)
    {
        await using var content = file?.OpenReadStream();
        var upload = file is null || content is null ? null : new UploadedFile(file.FileName, file.Length, content);
        return (await jobs.UploadCvAsync(LearnerId, upload, cancellationToken)).ToActionResult(this);
    }

    /// <summary>201 with the application. 409 <c>already-applied</c> / <c>reapply-cool-off</c> (30 days after withdrawing).</summary>
    [HttpPost("job/{id}/apply")]
    [Idempotent]
    public async Task<IActionResult> Apply(string id, ApplyRequest request, CancellationToken cancellationToken) =>
        (await jobs.ApplyAsync(LearnerId, id, request, cancellationToken))
            .ToActionResult(this, created => Created($"/api/v1/jobs/applications/{created.Id}", created));

    /// <summary>The last segment: accept · decline · suggest (another time, with a note). 410 once expired.</summary>
    [HttpPost("invitations/{id}/{response:regex(^(accept|decline|suggest)$)}")]
    [Idempotent]
    public async Task<IActionResult> Respond(string id, string response, RespondRequest request, CancellationToken cancellationToken) =>
        (await jobs.RespondAsync(LearnerId, id, response, request.Note, cancellationToken)).ToActionResult(this);

    /// <summary>BR-66: allowed until an offer.</summary>
    [HttpPost("applications/{id}/withdraw")]
    public async Task<IActionResult> Withdraw(string id, CancellationToken cancellationToken) =>
        (await jobs.WithdrawAsync(LearnerId, id, cancellationToken)).ToActionResult(this);

    [HttpGet("alerts")]
    public async Task<ActionResult<AlertSettingsDto>> Alerts(CancellationToken cancellationToken) =>
        await jobs.AlertsAsync(LearnerId, cancellationToken);

    [HttpPut("alerts")]
    public async Task<ActionResult<AlertSettingsDto>> SetAlerts(AlertSettingsDto request, CancellationToken cancellationToken) =>
        await jobs.SetAlertsAsync(LearnerId, request, cancellationToken);

    private ListQuery Query(string? q, string? place, string? sort, int? limit, string? filters) =>
        ListEngine.ParseQuery(q, place, sort, limit, filters, options.Value.MaxListLimit);
}
