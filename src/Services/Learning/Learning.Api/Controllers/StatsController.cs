using Asp.Versioning;
using Learning.Application.Contracts;
using Learning.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Learning.Api.Controllers;

/// <summary>API-130: My Stats → Learning (stats.api.ts).</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/stats")]
public sealed class StatsController(ILearningStatsService stats) : LearningControllerBase
{
    [HttpGet("learning")]
    public async Task<ActionResult<LearningDashboardDto>> Dashboard(CancellationToken cancellationToken) =>
        await stats.LearningAsync(LearnerId, cancellationToken);
}
