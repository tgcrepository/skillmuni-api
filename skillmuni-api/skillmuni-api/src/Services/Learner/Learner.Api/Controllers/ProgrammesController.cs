using Asp.Versioning;
using Learner.Application.Contracts;
using Learner.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace Learner.Api.Controllers;

/// <summary>API-122–124: the programme-code bonus quest (Figma section 13).</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}")]
public sealed class ProgrammesController(IProgrammeService programmes) : LearnerControllerBase
{
    /// <summary>404 unknown code; 429 too-many-tries after 5 wrong codes (30-minute cool-down).</summary>
    [HttpGet("programmes/{code}")]
    public async Task<IActionResult> Check(string code, CancellationToken cancellationToken) =>
        (await programmes.CheckAsync(CurrentLearner, code, cancellationToken)).ToActionResult(this);

    [HttpGet("banks/ifsc/{code}")]
    public async Task<IActionResult> Ifsc(string code, CancellationToken cancellationToken) =>
        (await programmes.FindIfscAsync(code, cancellationToken)).ToActionResult(this);

    [HttpPost("me/programme")]
    public async Task<IActionResult> Submit(ProgrammeSubmission submission, CancellationToken cancellationToken) =>
        (await programmes.SubmitAsync(CurrentLearner, submission, cancellationToken)).ToActionResult(this);
}

/// <summary>Tenant admin: the programme codes learners can enter. A separate controller so learners' policy doesn't apply.</summary>
[ApiController]
[ApiVersion(1.0)]
[Produces("application/json")]
[Route("api/v{version:apiVersion}/admin/programmes")]
[Authorize(Policy = Policies.TenantAdmin)]
public sealed class AdminProgrammesController(IProgrammeService programmes) : ControllerBase
{
    /// <summary>Create or replace a programme code (idempotent).</summary>
    [HttpPut("{code}")]
    public async Task<ActionResult<ProgrammeDto>> Save(string code, SaveProgrammeRequest request, CancellationToken cancellationToken) =>
        await programmes.SaveAsync(request with { Code = code }, cancellationToken);
}
