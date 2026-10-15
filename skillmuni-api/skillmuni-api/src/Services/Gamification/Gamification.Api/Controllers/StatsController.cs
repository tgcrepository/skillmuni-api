using Asp.Versioning;
using Gamification.Application;
using Gamification.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Common.Paging;
using SkillMuni.Web.Errors;

namespace Gamification.Api.Controllers;

/// <summary>API-129, 132, 133 and MISS-01/03/04.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}")]
public sealed class StatsController(IStatsService stats) : GamificationControllerBase
{
    [HttpGet("stats/rewards")]
    public async Task<ActionResult<RewardsDashboardDto>> Rewards(CancellationToken cancellationToken) =>
        await stats.RewardsAsync(LearnerId, cancellationToken);

    [HttpGet("badges")]
    public async Task<ActionResult<BadgeCatalogueDto>> Badges(CancellationToken cancellationToken) =>
        await stats.BadgesAsync(LearnerId, cancellationToken);

    /// <summary>Up to 3 earned badge ids, in showcase order.</summary>
    [HttpPut("badges/showcase")]
    public async Task<IActionResult> Showcase(ShowcaseRequest request, CancellationToken cancellationToken) =>
        (await stats.SetShowcaseAsync(LearnerId, request.Ids, cancellationToken)).ToActionResult(this);

    /// <summary>MISS-01: every XP credit, newest first.</summary>
    [HttpGet("me/xp/ledger")]
    public async Task<ActionResult<PagedResult<XpEntryDto>>> Ledger([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        await stats.LedgerAsync(LearnerId, PageRequest.Create(page, pageSize), cancellationToken);

    /// <summary>MISS-04.</summary>
    [HttpGet("me/streak")]
    public async Task<ActionResult<StreakDetailDto>> Streak(CancellationToken cancellationToken) =>
        await stats.StreakAsync(LearnerId, cancellationToken);

    /// <summary>MISS-03: today's three quests; completed ones are credited automatically.</summary>
    [HttpGet("me/daily-quests")]
    public async Task<ActionResult<DailyQuestsDto>> DailyQuests(CancellationToken cancellationToken) =>
        await stats.DailyQuestsAsync(LearnerId, cancellationToken);
}
