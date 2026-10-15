using Microsoft.Extensions.Options;

namespace Assessment.Application.Services;

/// <summary>API-131: My Stats → Assessments ("Skill Stats", Figma 161:4439, empty state 161:4514).</summary>
public interface IAssessmentStatsService
{
    AssessmentsDashboardDto Dashboard();
}

/// <summary>
/// The slide lists scored skill assessments with tiers. None exist in Phase 1 (Career Drivers has no score or tier), so
/// every learner gets the empty state with Muni's pick, as the mock's <c>?stats=empty</c> default. When a skill
/// assessment engine lands, its results fill <c>results</c> here.
/// </summary>
public sealed class AssessmentStatsService(IOptions<AssessmentOptions> options) : IAssessmentStatsService
{
    public AssessmentsDashboardDto Dashboard()
    {
        var stats = options.Value.Stats;
        return new AssessmentsDashboardDto(
            [],
            stats.Available,
            stats.FirstCardXp,
            new RecommendedAssessmentDto(stats.RecommendedTitle, stats.RecommendedUrl, stats.RecommendedWhy));
    }
}
