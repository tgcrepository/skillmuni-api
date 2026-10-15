using Gamification.Application.Rules;
using Gamification.Domain;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Paging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Gamification.Application.Services;

/// <summary>
/// API-129 (Rewards · Player HQ), API-132/133 (badge library and showcase), MISS-01 (XP ledger), MISS-03 (daily
/// quests), MISS-04 (streak). My Stats' Learning and Assessments tabs belong to those services.
/// </summary>
public interface IStatsService
{
    Task<RewardsDashboardDto> RewardsAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<BadgeCatalogueDto> BadgesAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<Result<BadgeCatalogueDto>> SetShowcaseAsync(Guid learnerId, IReadOnlyList<string> ids, CancellationToken cancellationToken);

    Task<PagedResult<XpEntryDto>> LedgerAsync(Guid learnerId, PageRequest page, CancellationToken cancellationToken);

    Task<StreakDetailDto> StreakAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<DailyQuestsDto> DailyQuestsAsync(Guid learnerId, CancellationToken cancellationToken);
}

public sealed class StatsService(
    IPlayerRepository players,
    IXpLedger ledger,
    IBadgeAwardRepository awards,
    IActivityRepository activity,
    IBoardQueries boards,
    IGameEngine engine,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<GamificationOptions> options) : IStatsService
{
    private const int UpNextCount = 3;

    private GamificationOptions Rules => options.Value;

    public async Task<RewardsDashboardDto> RewardsAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var player = await PlayerOrNewAsync(learnerId, cancellationToken);
        var today = engine.Today(player);
        var level = engine.LevelFor(player.TotalXp);

        int? batchRank = null;
        if (player.Batch is { } batch)
        {
            var mates = await players.ListBatchAsync(batch, cancellationToken);
            batchRank = (await boards.RankOfAsync(learnerId, null, mates, cancellationToken))?.Rank;
        }

        RankDto? rank = null;
        if (await boards.RankOfAsync(learnerId, null, null, cancellationToken) is { } mine && mine.Rank > 1)
        {
            var above = await boards.TopAsync(null, null, null, mine.Rank - 1, cancellationToken);
            if (above.Count == mine.Rank - 1)
            {
                var next = above[^1];
                var nextPlayer = (await players.GetManyAsync([next.LearnerId], cancellationToken)).GetValueOrDefault(next.LearnerId);
                rank = new RankDto(mine.Rank, Math.Max(1, next.Xp - mine.Row.Xp), PlayerViews.FirstName(nextPlayer?.Name ?? "Learner"));
            }
        }

        var catalogue = await BadgesAsync(player, cancellationToken);
        var pinned = player.Showcase
            .Select(id => catalogue.Badges.FirstOrDefault(b => b.Id == id && b.State == "earned"))
            .Where(b => b is not null)
            .Select(b => new ShowcaseSlotDto(b!.Id, b.Name, b.Icon, b.Rarity))
            .ToList();
        var slots = pinned.Cast<ShowcaseSlotDto?>().Concat(Enumerable.Repeat<ShowcaseSlotDto?>(null, Player.MaxShowcase)).Take(Player.MaxShowcase).ToList();
        var upNext = catalogue.Badges
            .Where(b => b.State == "progress")
            .OrderByDescending(b => b.Percent)
            .Take(UpNextCount)
            .Select(b => new BadgeProgressDto(b.Id, b.Name, b.Icon, b.ProgressLabel ?? string.Empty, b.Percent))
            .ToList();

        var monday = PlayerViews.Monday(today);
        var days = await activity.ListDaysAsync(learnerId, monday, monday.AddDays(6), cancellationToken);

        return new RewardsDashboardDto(
            new RewardsPlayerDto(player.Name, player.Initials, level.Rank, player.PhotoUrl, level.Level, player.TotalXp, level.Target, batchRank),
            new StreakDto(player.CurrentStreak(today), PlayerViews.Week(today, days)),

            // A-302: coins aren't earned in Phase 1.
            new WalletDto(player.TotalXp, 0, 0, 0, 0),
            await DailyAsync(player, today, cancellationToken),
            new RewardsBadgesDto(catalogue.Earned, catalogue.Total, slots, upNext),
            rank,
            Math.Round(player.LearningMinutes / 60.0, 1));
    }

    public async Task<BadgeCatalogueDto> BadgesAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await BadgesAsync(await PlayerOrNewAsync(learnerId, cancellationToken), cancellationToken);

    public async Task<Result<BadgeCatalogueDto>> SetShowcaseAsync(Guid learnerId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        if (ids.Count > Player.MaxShowcase)
        {
            return GamificationErrors.TooManyPinned;
        }

        var earned = await awards.ListAsync(learnerId, cancellationToken);
        if (ids.Any(id => !earned.ContainsKey(id)))
        {
            return GamificationErrors.BadgeNotEarned;
        }

        var player = await engine.PlayerAsync(learnerId, null, null, cancellationToken);
        player.SetShowcase(ids, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await BadgesAsync(player, cancellationToken);
    }

    public async Task<PagedResult<XpEntryDto>> LedgerAsync(Guid learnerId, PageRequest page, CancellationToken cancellationToken) =>
        (await ledger.ListAsync(learnerId, page, cancellationToken))
            .Map(e => new XpEntryDto(e.Reason, PlayerViews.ReasonLabel(e.Reason), e.SourceId, e.Xp, DateTime.SpecifyKind(e.OccurredAt, DateTimeKind.Utc)));

    public async Task<StreakDetailDto> StreakAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var player = await PlayerOrNewAsync(learnerId, cancellationToken);
        var today = engine.Today(player);
        var monday = PlayerViews.Monday(today);
        var days = await activity.ListDaysAsync(learnerId, monday, monday.AddDays(6), cancellationToken);
        return new StreakDetailDto(player.CurrentStreak(today), player.BestStreak, PlayerViews.Week(today, days), Rules.Arena.StreakBonus);
    }

    public async Task<DailyQuestsDto> DailyQuestsAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var player = await PlayerOrNewAsync(learnerId, cancellationToken);
        return await DailyAsync(player, engine.Today(player), cancellationToken);
    }

    private async Task<DailyQuestsDto> DailyAsync(Player player, DateOnly today, CancellationToken cancellationToken) =>
        PlayerViews.Daily(Rules.Dailies(), await activity.ListDailyAsync(player.Id, today, cancellationToken), PlayerViews.NextMidnight(today, engine.TimeZone(player)));

    private async Task<BadgeCatalogueDto> BadgesAsync(Player player, CancellationToken cancellationToken)
    {
        var earned = await awards.ListAsync(player.Id, cancellationToken);
        var owners = await awards.OwnershipAsync(cancellationToken);
        var count = await players.CountAsync(cancellationToken);
        var badges = BadgeCatalogue.All(Rules.QuestBadges())
            .Select(b => PlayerViews.Badge(b, player, Rules, earned, owners, count))
            .ToList();
        return new BadgeCatalogueDto(badges.Count(b => b.State == "earned"), badges.Count, badges);
    }

    /// <summary>Reads don't create rows: a learner without a player yet sees a fresh one.</summary>
    private async Task<Player> PlayerOrNewAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await players.GetAsync(learnerId, cancellationToken)
        ?? Player.Join(learnerId, tenant.TenantId, tenant.Market, string.Empty, clock.GetUtcNow().UtcDateTime);
}
