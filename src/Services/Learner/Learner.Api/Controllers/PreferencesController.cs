using Asp.Versioning;
using Learner.Application.Contracts;
using Learner.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Learner.Api.Controllers;

/// <summary>MISS-09: sounds, voice and language.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/me/preferences")]
public sealed class PreferencesController(IPreferencesService preferences) : LearnerControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PreferencesDto>> Get(CancellationToken cancellationToken) =>
        await preferences.GetAsync(CurrentLearner, cancellationToken);

    [HttpPut]
    public async Task<ActionResult<PreferencesDto>> Save(PreferencesRequest request, CancellationToken cancellationToken) =>
        await preferences.SaveAsync(CurrentLearner, request, cancellationToken);
}
