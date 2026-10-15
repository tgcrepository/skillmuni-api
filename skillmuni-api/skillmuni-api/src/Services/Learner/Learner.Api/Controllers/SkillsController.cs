using Asp.Versioning;
using Learner.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;

namespace Learner.Api.Controllers;

/// <summary>API-057 / A-245: ESCO skill search for Quest 4's decks.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/skills")]
public sealed class SkillsController(ISkillSearchService skills) : LearnerControllerBase
{
    [HttpGet("esco")]
    public async Task<IActionResult> Esco([FromQuery] string? q, [FromQuery] string? lang, [FromQuery] int? limit, CancellationToken cancellationToken) =>
        (await skills.SearchAsync(q, lang, limit, cancellationToken)).ToActionResult(this);
}
