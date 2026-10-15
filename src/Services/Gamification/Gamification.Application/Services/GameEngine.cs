using Gamification.Application.Rules;
using Gamification.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Tenancy;

namespace Gamification.Application.Services;

public sealed record LevelInfo(int Level, string Rank, int Start, int Target);

/// <summary>
/// The only place XP is credited (CLAUDE.md §9). Each call works on tracked entities; the caller (an event handler
/// or a use case) saves them in one transaction with the outbox rows, so totals, ledger and events never disagree.
/// </summary>
public interface IGameEngine
{
    /// <summary>The player, created on first contact (events can arrive before the registration event).</summary>
    Task<Player> PlayerAsync(Guid learnerId, string? name, string? market, CancellationToken cancellationToken);

    /// <returns>The XP credited: 0 when this reason + source already paid.</returns>
    Task<int> AwardAsync(Player player, string reason, string sourceId, int xp, CancellationToken cancellationToken);

    /// <summary>
    /// §4.4 / B-45: counts <paramref name="day"/> as a streak day (a brief quiz submitted for the first time that day).
    /// No XP (BR-139). A day older than the last streak day (a late event) changes nothing.
    /// </summary>
    Task MarkStreakDayAsync(Player player, DateOnly day, CancellationToken cancellationToken);

    /// <summary>Moves the daily quests of this kind for <paramref name="day"/>; a completed one credits its XP.</summary>
    Task ProgressDailyAsync(Player player, string kind, int amount, DateOnly day, CancellationToken cancellationToken);

    /// <summary>Awards every badge whose rule is now met and queues the totals event. Call last.</summary>
    Task FinishAsync(Player player, CancellationToken cancellationToken);

    LevelInfo LevelFor(int xp);

    DateOnly Today(Player player);

    /// <summary>The learner's local date of an event (its <c>occurredAt</c>; now when the publisher left it empty).</summary>
    DateOnly DayOf(Player player, DateTime occurredAtUtc);

    TimeZoneInfo TimeZone(Player player);
}

public sealed partial class GameEngine(
    IPlayerRepository players,
    IXpLedger ledger,
    IBadgeAwardRepository awards,
    IActivityRepository activity,
    IOutbox outbox,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<GamificationOptions> options,
    ILogger<GameEngine> logger) : IGameEngine
{
    private readonly HashSet<Guid> _changed = [];

    private GamificationOptions Rules => options.Value;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<Player> PlayerAsync(Guid learnerId, string? name, string? market, CancellationToken cancellationToken)
    {
        var player = await players.GetAsync(learnerId, cancellationToken);
        if (player is null)
        {
            player = Player.Join(learnerId, tenant.TenantId, market ?? tenant.Market, name ?? string.Empty, Now);
            players.Add(player);
        }
        else if (name is not null && player.Name.Length == 0)
        {
            player.Rename(name, Now);
        }

        return player;
    }

    public async Task<int> AwardAsync(Player player, string reason, string sourceId, int xp, CancellationToken cancellationToken)
    {
        if (xp <= 0 || await ledger.ExistsAsync(player.Id, reason, sourceId, cancellationToken))
        {
            return 0;
        }

        ledger.Add(XpEntry.Credit(player.TenantId, player.Id, reason, sourceId, xp, Now));
        player.AddXp(xp, Now);
        _changed.Add(player.Id);
        LogAwarded(logger, player.Id, reason, xp);
        return xp;
    }

    public Task MarkStreakDayAsync(Player player, DateOnly day, CancellationToken cancellationToken)
    {
        if (player.MarkActive(day, Now))
        {
            activity.Add(ActiveDay.On(player.TenantId, player.Id, day));
            _changed.Add(player.Id);
        }

        return Task.CompletedTask;
    }

    public async Task ProgressDailyAsync(Player player, string kind, int amount, DateOnly day, CancellationToken cancellationToken)
    {
        var quests = Rules.Dailies().Where(q => q.Kind == kind).ToList();
        if (quests.Count == 0 || amount <= 0)
        {
            return;
        }

        var existing = await activity.ListDailyAsync(player.Id, day, cancellationToken);
        foreach (var quest in quests)
        {
            var progress = existing.FirstOrDefault(p => p.QuestId == quest.Id);
            if (progress is null)
            {
                progress = DailyProgress.Start(player.TenantId, player.Id, day, quest.Id);
                activity.Add(progress);
            }

            if (progress.Add(amount, quest.Target))
            {
                await AwardAsync(player, $"daily.{quest.Id}", day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), quest.Xp, cancellationToken);
            }
        }
    }

    public async Task FinishAsync(Player player, CancellationToken cancellationToken)
    {
        var earned = await awards.ListAsync(player.Id, cancellationToken);
        var catalogue = BadgeCatalogue.All(Rules.QuestBadges());

        // §4.3: Phase 1 awards the quest badges only; catalogue badges wait for Gamification:Badges:AwardCatalogue.
        var awardable = catalogue.Where(b => b.Tracked && !earned.ContainsKey(b.Id) && (b.IsQuest || Rules.Badges.AwardCatalogue));
        foreach (var badge in awardable)
        {
            if (BadgeProgress.Value(player, badge, Rules) >= badge.Goal)
            {
                awards.Add(BadgeAward.Earn(player.TenantId, player.Id, badge.Id, Now));
                player.BadgeEarned(Now);
                outbox.Add(new BadgeEarned(player.Id, badge.Id, badge.Name) { TenantId = player.TenantId });
                _changed.Add(player.Id);
                LogBadge(logger, player.Id, badge.Id);
            }
        }

        if (_changed.Remove(player.Id))
        {
            var level = LevelFor(player.TotalXp);
            outbox.Add(new XpAwarded(player.Id, player.TotalXp, level.Level, level.Rank, level.Start, level.Target, player.Badges, player.CurrentStreak(Today(player)))
            {
                TenantId = player.TenantId,
            });
        }
    }

    public LevelInfo LevelFor(int xp)
    {
        var ladder = Rules.Ladder();
        var step = ladder.LastOrDefault(s => xp >= s.Min) ?? ladder[0];
        var next = ladder.FirstOrDefault(s => s.Min > xp);
        return new LevelInfo(step.Level, step.Rank, step.Min, next?.Min ?? step.Min + Rules.LastLevelSpan);
    }

    public TimeZoneInfo TimeZone(Player player) => Rules.TimeZoneFor(player.Market);

    public DateOnly Today(Player player) => DayOf(player, Now);

    public DateOnly DayOf(Player player, DateTime occurredAtUtc)
    {
        var at = occurredAtUtc == default ? Now : DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(at, TimeZone(player)));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "XP awarded {LearnerId} {Reason} {Xp}")]
    private static partial void LogAwarded(ILogger logger, Guid learnerId, string reason, int xp);

    [LoggerMessage(Level = LogLevel.Information, Message = "Badge earned {LearnerId} {BadgeId}")]
    private static partial void LogBadge(ILogger logger, Guid learnerId, string badgeId);
}

/// <summary>A badge's current count for a player (counters, plus the derived ones).</summary>
public static class BadgeProgress
{
    public static int Value(Player player, BadgeDefinition badge, GamificationOptions rules) => badge.Counter switch
    {
        null => 0,
        Counters.TotalXp => player.TotalXp,
        Counters.BestStreak => player.BestStreak,
        Counters.Founding => DateOnly.FromDateTime(player.JoinedAt) < rules.FoundingUntil ? 1 : 0,
        var key => player.Counter(key),
    };
}
