using Asp.Versioning;
using Assessment.Application;
using Assessment.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Assessment.Api.Controllers;

/// <summary>API-131: My Stats → Assessments.</summary>
[ApiVersion(1.0)]
public sealed class StatsController(IAssessmentStatsService stats) : AssessmentControllerBase
{
    [HttpGet("stats/assessments")]
    public ActionResult<AssessmentsDashboardDto> Assessments() => stats.Dashboard();
}
