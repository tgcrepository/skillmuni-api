using Gamification.Application.Rules;
using Gamification.Domain;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Gamification.Application.Services;

/// <summary>API-001–003: the Arena (Figma Leaderboard v2). Boards are computed from the XP ledger.</summary>
public interface IArenaService
{
    Task<Result<ArenaBoardDto>> BoardAsync(Guid learnerId, string scope, string period, CancellationToken cancellationToken);

    Task<Result<CheersDto>> CheerAsync(Guid learnerId, Guid playerId, bool on, CancellationToken cancellationToken);

    Task<Result> ChallengeAsync(Guid learnerId, Guid playerId, CancellationToken cancellationToken);
}

public sealed class ArenaService(
    IPlayerRepository players,
    IBoardQueries boards,
    IActivityRepository activity,
    ISocialRepository social,
    IGameEngine engine,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<GamificationOptions> options) : IArenaService
{
    private static readonly string[] Scopes = ["everyone", "batch", "friends"];
    private static readonly string[] Periods = ["week", "month", "all"];

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<Result<ArenaBoardDto>> BoardAsync(Guid learnerId, string scope, string period, CancellationToken cancellationToken)
    {
        if (!Scopes.Contains(scope, StringComparer.Ordinal))
        {
            return GamificationErrors.InvalidScope;
        }

        if (!Periods.Contains(period, StringComparer.Ordinal))
        {
            return GamificationErrors.InvalidPeriod;
        }

        var arena = options.Value.Arena;
        var stored = await players.GetAsync(learnerId, cancellationToken);
        var me = stored ?? Player.Join(learnerId, tenant.TenantId, tenant.Market, string.Empty, Now);
        var zone = engine.TimeZone(me);
        var today = engine.Today(me);
        DateTime? since = period switch
        {
            "week" => PlayerViews.StartOf(PlayerViews.Monday(today), zone),
            "month" => PlayerViews.StartOf(new DateOnly(today.Year, today.Month, 1), zone),
            _ => null,
        };

        var cheered = await social.CheeredByAsync(learnerId, cancellationToken);
        IReadOnlyCollection<Guid>? only = scope switch
        {
            "batch" => me.Batch is null ? [learnerId] : await players.ListBatchAsync(me.Batch, cancellationToken),
            "friends" => cheered.Append(learnerId).ToList(),
            _ => null,
        };

        var top = await boards.TopAsync(since, null, only, arena.BoardSize, cancellationToken);
        var yesterday = await boards.TopAsync(since, PlayerViews.StartOf(today, zone), only, arena.BoardSize * 2, cancellationToken);
        var rankYesterday = yesterday.Select((r, i) => (r.LearnerId, Rank: i + 1)).ToDictionary(x => x.LearnerId, x => x.Rank);

        var ranked = top.Select((r, i) => (Row: r, Rank: i + 1)).ToList();
        if (ranked.All(r => r.Row.LearnerId != learnerId) && await boards.RankOfAsync(learnerId, since, only, cancellationToken) is { } mine)
        {
            ranked.Add(mine);
        }

        var people = await players.GetManyAsync(ranked.Select(r => r.Row.LearnerId).ToList(), cancellationToken);
        var cheeredSet = cheered.ToHashSet();
        var rows = ranked.Select(r =>
        {
            var p = people.GetValueOrDefault(r.Row.LearnerId);
            var level = engine.LevelFor(p?.TotalXp ?? 0);
            var movement = rankYesterday.TryGetValue(r.Row.LearnerId, out var before) ? before - r.Rank : 0;
            var name = p?.Name is { Length: > 0 } n ? n : "Learner";
            return new ArenaPlayerDto(
                r.Row.LearnerId.ToString(),
                r.Rank,
                name,
                p?.Initials is { Length: > 0 } i ? i : "?",
                p?.PhotoUrl,
                r.Row.Xp,
                p?.CurrentStreak(today) ?? 0,
                p?.Counter(Counters.ModulesCompleted) ?? 0,
                movement,
                r.Row.LearnerId == learnerId,
                level.Rank,
                level.Level,
                p?.Badges ?? 0,
                string.Empty,
                p?.CheersReceived ?? 0,
                cheeredSet.Contains(r.Row.LearnerId));
        }).ToList();

        var you = rows.FirstOrDefault(r => r.You);
        var above = you is null ? null : rows.FirstOrDefault(r => r.Rank == you.Rank - 1);
        var meDto = new ArenaMeDto(
            you?.Rank ?? 0,
            you?.Xp ?? 0,
            you?.Movement ?? 0,
            me.CurrentStreak(today),
            above?.Name,
            above?.Xp,
            rows.FirstOrDefault()?.Xp ?? 0);

        // "You passed X": only on the default tab, compared with the rank the learner saw last time.
        RankUpDto? rankUp = null;
        if (scope == "everyone" && period == "week" && you is not null && stored is not null)
        {
            if (stored.LastSeenRank is { } last && you.Rank < last && rows.FirstOrDefault(r => r.Rank == you.Rank + 1) is { } passed)
            {
                rankUp = new RankUpDto(you.Rank, PlayerViews.FirstName(passed.Name));
            }

            // Its own single-column UPDATE: the read must never fail on a concurrent event saving this player.
            if (stored.LastSeenRank != you.Rank)
            {
                await players.RememberSeenRankAsync(learnerId, you.Rank, cancellationToken);
            }
        }

        var monday = PlayerViews.Monday(today);
        var days = await activity.ListDaysAsync(learnerId, monday, monday.AddDays(6), cancellationToken);
        return new ArenaBoardDto(
            scope,
            period,
            rows,
            meDto,
            PlayerViews.Week(today, days),
            arena.PromotionRank,
            arena.League,
            rankUp,
            ArenaRewardsDto.None);
    }

    public async Task<Result<CheersDto>> CheerAsync(Guid learnerId, Guid playerId, bool on, CancellationToken cancellationToken)
    {
        if (learnerId == playerId)
        {
            return GamificationErrors.NotYourself;
        }

        var target = await players.GetAsync(playerId, cancellationToken);
        if (target is null)
        {
            return GamificationErrors.PlayerNotFound;
        }

        var cheer = await social.FindCheerAsync(learnerId, playerId, cancellationToken);
        if (cheer is null)
        {
            if (on)
            {
                social.Add(Cheer.Give(tenant.TenantId, learnerId, playerId, Now));
                target.Cheered(true, Now);

                // Only a first cheer for a player counts towards Cheerleader.
                var me = await engine.PlayerAsync(learnerId, null, null, cancellationToken);
                me.Count(Counters.CheersGiven, 1, Now);
                await engine.FinishAsync(me, cancellationToken);
            }
        }
        else if (cheer.Set(on, Now))
        {
            target.Cheered(on, Now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CheersDto(target.CheersReceived);
    }

    public async Task<Result> ChallengeAsync(Guid learnerId, Guid playerId, CancellationToken cancellationToken)
    {
        if (learnerId == playerId)
        {
            return GamificationErrors.NotYourself;
        }

        if (await players.GetAsync(playerId, cancellationToken) is null)
        {
            return GamificationErrors.PlayerNotFound;
        }

        if (await social.OpenChallengeExistsAsync(learnerId, playerId, Now, cancellationToken))
        {
            return GamificationErrors.ChallengeOpen;
        }

        social.Add(Challenge.Invite(tenant.TenantId, learnerId, playerId, Now, options.Value.Arena.ChallengeLength));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
