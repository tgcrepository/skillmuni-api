using Gamification.Domain;
using SkillMuni.Common.Paging;

namespace Gamification.Application;

// Every query is scoped to the current tenant by the DbContext.

public interface IPlayerRepository
{
    Task<Player?> GetAsync(Guid learnerId, CancellationToken cancellationToken);

    /// <summary>Names, photos and stats for the board rows, in one query.</summary>
    Task<IReadOnlyDictionary<Guid, Player>> GetManyAsync(IReadOnlyCollection<Guid> learnerIds, CancellationToken cancellationToken);

    /// <summary>Learners in a batch (the Arena "batch" tab).</summary>
    Task<IReadOnlyList<Guid>> ListBatchAsync(string batch, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    void Add(Player player);
}

public interface IXpLedger
{
    Task<bool> ExistsAsync(Guid learnerId, string reason, string sourceId, CancellationToken cancellationToken);

    /// <summary>When this reason last paid the learner, if ever (BR-114's 30-day rule).</summary>
    Task<DateTime?> LastCreditAsync(Guid learnerId, string reason, CancellationToken cancellationToken);

    /// <summary>The learner's one XP total: the sum of their ledger (BR-138).</summary>
    Task<int> TotalAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<PagedResult<XpEntry>> ListAsync(Guid learnerId, PageRequest page, CancellationToken cancellationToken);

    void Add(XpEntry entry);
}

public interface IBadgeAwardRepository
{
    Task<IReadOnlyDictionary<string, DateTime>> ListAsync(Guid learnerId, CancellationToken cancellationToken);

    /// <summary>Badge id → how many players in the tenant own it (for "owned by N%").</summary>
    Task<IReadOnlyDictionary<string, int>> OwnershipAsync(CancellationToken cancellationToken);

    void Add(BadgeAward award);
}

public interface IActivityRepository
{
    Task<IReadOnlyList<DateOnly>> ListDaysAsync(Guid learnerId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    void Add(ActiveDay day);

    Task<IReadOnlyList<DailyProgress>> ListDailyAsync(Guid learnerId, DateOnly day, CancellationToken cancellationToken);

    void Add(DailyProgress progress);
}

public interface ISocialRepository
{
    Task<Cheer?> FindCheerAsync(Guid from, Guid to, CancellationToken cancellationToken);

    /// <summary>Who this learner is cheering now (also the Arena "friends" tab until friends exist).</summary>
    Task<IReadOnlyList<Guid>> CheeredByAsync(Guid from, CancellationToken cancellationToken);

    Task<bool> OpenChallengeExistsAsync(Guid from, Guid to, DateTime now, CancellationToken cancellationToken);

    void Add(Cheer cheer);

    void Add(Challenge challenge);
}

/// <summary>One row of a board: XP in the window and when the learner last earned (ties go to the earlier).</summary>
public sealed record BoardRow(Guid LearnerId, int Xp, DateTime LastEarnedAt);

/// <summary>Leaderboard queries over the ledger (index on tenant + occurred_at).</summary>
public interface IBoardQueries
{
    /// <param name="since">Window start (null = all time).</param>
    /// <param name="before">Window end, exclusive (null = now). Used for yesterday's ranks.</param>
    /// <param name="only">Restrict to these learners (batch, friends); null = everyone in the tenant.</param>
    Task<IReadOnlyList<BoardRow>> TopAsync(DateTime? since, DateTime? before, IReadOnlyCollection<Guid>? only, int limit, CancellationToken cancellationToken);

    /// <summary>The learner's row and 1-based rank, or null when they earned nothing in the window.</summary>
    Task<(BoardRow Row, int Rank)?> RankOfAsync(Guid learnerId, DateTime? since, IReadOnlyCollection<Guid>? only, CancellationToken cancellationToken);

    /// <summary>How many learners earned XP in the window.</summary>
    Task<int> CountAsync(DateTime? since, IReadOnlyCollection<Guid>? only, CancellationToken cancellationToken);
}

/// <summary>MISS-08 erasure: every row this service holds for one learner. Safe to run twice.</summary>
public interface IPlayerDataEraser
{
    Task EraseAsync(Guid learnerId, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
