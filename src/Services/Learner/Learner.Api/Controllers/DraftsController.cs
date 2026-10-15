using System.Text.Json;
using Asp.Versioning;
using Learner.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;

namespace Learner.Api.Controllers;

/// <summary>API-034/035: offline-queue autosave. The body is any JSON value.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/drafts")]
public sealed class DraftsController(IDraftService drafts) : LearnerControllerBase
{
    [HttpPut("{key}")]
    [RequestSizeLimit(128 * 1024)]
    public async Task<IActionResult> Save(string key, [FromBody] JsonElement value, CancellationToken cancellationToken) =>
        (await drafts.SaveAsync(CurrentLearnerId, key, value, cancellationToken)).ToActionResult(this);

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, CancellationToken cancellationToken) =>
        (await drafts.GetAsync(CurrentLearnerId, key, cancellationToken)).ToActionResult(this);
}
