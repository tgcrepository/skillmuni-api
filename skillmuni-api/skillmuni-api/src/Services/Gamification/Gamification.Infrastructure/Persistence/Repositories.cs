using Gamification.Application;
using Gamification.Domain;
using Microsoft.EntityFrameworkCore;
using SkillMuni.Common.Paging;

namespace Gamification.Infrastructure.Persistence;

internal sealed class PlayerRepository(GamificationDbContext db) : IPlayerRepository
{
    public Task<Player?> GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Players.FirstOrDefaultAsync(p => p.Id == learnerId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Player>> GetManyAsync(IReadOnlyCollection<Guid> learnerIds, CancellationToken cancellationToken) =>
        learnerIds.Count == 0
            ? new Dictionary<Guid, Player>()
            : await db.Players.AsNoTracking().Where(p => learnerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListBatchAsync(string batch, CancellationToken cancellationToken) =>
        await db.Players.AsNoTracking().Where(p => p.Batch == batch).Select(p => p.Id).ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken) => db.Players.CountAsync(cancellationToken);

    public void Add(Player player) => db.Players.Add(player);
}

internal sealed class XpLedger(GamificationDbContext db) : IXpLedger
{
    public Task<bool> ExistsAsync(Guid learnerId, string reason, string sourceId, CancellationToken cancellationToken) =>
        db.XpEntries.AsNoTracking().AnyAsync(e => e.LearnerId == learnerId && e.Reason == reason && e.SourceId == sourceId, cancellationToken);

    public async Task<PagedResult<XpEntry>> ListAsync(Guid learnerId, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.XpEntries.AsNoTracking().Where(e => e.LearnerId == learnerId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(e => e.OccurredAt).Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<XpEntry>(items, page.Page, page.PageSize, total);
    }

    public void Add(XpEntry entry) => db.XpEntries.Add(entry);
}

internal sealed class BadgeAwardRepository(GamificationDbContext db) : IBadgeAwardRepository
{
    public async Task<IReadOnlyDictionary<string, DateTime>> ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await db.BadgeAwards.AsNoTracking().Where(a => a.LearnerId == learnerId)
            .ToDictionaryAsync(a => a.BadgeId, a => a.EarnedAt, StringComparer.Ordinal, cancellationToken);

    public async Task<IReadOnlyDictionary<string, int>> OwnershipAsync(CancellationToken cancellationToken) =>
        await db.BadgeAwards.AsNoTracking()
            .GroupBy(a => a.BadgeId)
            .Select(g => new { BadgeId = g.Key, Owners = g.Count() })
            .ToDictionaryAsync(x => x.BadgeId, x => x.Owners, StringComparer.Ordinal, cancellationToken);

    public void Add(BadgeAward award) => db.BadgeAwards.Add(award);
}

internal sealed class ActivityRepository(GamificationDbContext db) : IActivityRepository
{
    public async Task<IReadOnlyList<DateOnly>> ListDaysAsync(Guid learnerId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await db.ActiveDays.AsNoTracking().Where(d => d.LearnerId == learnerId && d.Day >= from && d.Day <= to).Select(d => d.Day).ToListAsync(cancellationToken);

    public void Add(ActiveDay day) => db.ActiveDays.Add(day);

    public async Task<IReadOnlyList<DailyProgress>> ListDailyAsync(Guid learnerId, DateOnly day, CancellationToken cancellationToken) =>
        await db.DailyProgress.Where(d => d.LearnerId == learnerId && d.Day == day).ToListAsync(cancellationToken);

    public void Add(DailyProgress progress) => db.DailyProgress.Add(progress);
}

internal sealed class SocialRepository(GamificationDbContext db) : ISocialRepository
{
    public Task<Cheer?> FindCheerAsync(Guid from, Guid to, CancellationToken cancellationToken) =>
        db.Cheers.FirstOrDefaultAsync(c => c.FromLearnerId == from && c.ToLearnerId == to, cancellationToken);

    public async Task<IReadOnlyList<Guid>> CheeredByAsync(Guid from, CancellationToken cancellationToken) =>
        await db.Cheers.AsNoTracking().Where(c => c.FromLearnerId == from && c.Active).Select(c => c.ToLearnerId).ToListAsync(cancellationToken);

    public Task<bool> OpenChallengeExistsAsync(Guid from, Guid to, DateTime now, CancellationToken cancellationToken) =>
        db.Challenges.AsNoTracking().AnyAsync(c => c.FromLearnerId == from && c.ToLearnerId == to && c.EndsAt > now, cancellationToken);

    public void Add(Cheer cheer) => db.Cheers.Add(cheer);

    public void Add(Challenge challenge) => db.Challenges.Add(challenge);
}

/// <summary>
/// Boards: XP summed per learner over a window of the ledger, ties to whoever got there first. One grouped query per
/// board (index on tenant + occurred_at); the shared "everyone" boards are cached by <see cref="CachedBoardQueries"/>.
/// </summary>
internal sealed class BoardQueries(GamificationDbContext db) : IBoardQueries
{
    public async Task<IReadOnlyList<BoardRow>> TopAsync(DateTime? since, DateTime? before, IReadOnlyCollection<Guid>? only, int limit, CancellationToken cancellationToken)
    {
        var rows = await Grouped(since, before, only)
            .OrderByDescending(r => r.Xp)
            .ThenBy(r => r.LastEarnedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return rows.Select(r => r.ToRow()).ToList();
    }

    public async Task<(BoardRow Row, int Rank)?> RankOfAsync(Guid learnerId, DateTime? since, IReadOnlyCollection<Guid>? only, CancellationToken cancellationToken)
    {
        var grouped = Grouped(since, null, only);
        var mine = await grouped.FirstOrDefaultAsync(r => r.LearnerId == learnerId, cancellationToken);
        if (mine is null)
        {
            return null;
        }

        var ahead = await grouped.CountAsync(r => r.Xp > mine.Xp || (r.Xp == mine.Xp && r.LastEarnedAt < mine.LastEarnedAt), cancellationToken);
        return (mine.ToRow(), ahead + 1);
    }

    private IQueryable<Aggregate> Grouped(DateTime? since, DateTime? before, IReadOnlyCollection<Guid>? only)
    {
        var entries = db.XpEntries.AsNoTracking();
        if (since is { } from)
        {
            entries = entries.Where(e => e.OccurredAt >= from);
        }

        if (before is { } to)
        {
            entries = entries.Where(e => e.OccurredAt < to);
        }

        if (only is not null)
        {
            var ids = only.ToList();
            entries = entries.Where(e => ids.Contains(e.LearnerId));
        }

        // Member-init (not a constructor) so later operators can still be translated to SQL.
        return entries
            .GroupBy(e => e.LearnerId)
            .Select(g => new Aggregate { LearnerId = g.Key, Xp = g.Sum(e => e.Xp), LastEarnedAt = g.Max(e => e.OccurredAt) });
    }

    private sealed class Aggregate
    {
        public Guid LearnerId { get; init; }

        public int Xp { get; init; }

        public DateTime LastEarnedAt { get; init; }

        public BoardRow ToRow() => new(LearnerId, Xp, DateTime.SpecifyKind(LastEarnedAt, DateTimeKind.Utc));
    }
}

/// <summary>MISS-08. Set-based deletes in one transaction (inside the retrying execution strategy).</summary>
internal sealed class PlayerDataEraser(GamificationDbContext db) : IPlayerDataEraser
{
    public Task EraseAsync(Guid learnerId, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(
            async ct =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.XpEntries.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.BadgeAwards.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.ActiveDays.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.DailyProgress.Where(x => x.LearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.Cheers.Where(x => x.FromLearnerId == learnerId || x.ToLearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.Challenges.Where(x => x.FromLearnerId == learnerId || x.ToLearnerId == learnerId).ExecuteDeleteAsync(ct);
                await db.Players.Where(x => x.Id == learnerId).ExecuteDeleteAsync(ct);
                await transaction.CommitAsync(ct);
            },
            cancellationToken);
}
