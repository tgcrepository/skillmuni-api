using Gamification.Application;
using Gamification.Application.Services;
using Gamification.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Paging;
using SkillMuni.Common.Tenancy;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Gamification.UnitTests;

/// <summary>Every repository in memory. Adds are visible at once (no separate save step).</summary>
public sealed class GameStore :
    IPlayerRepository, IXpLedger, IBadgeAwardRepository, IActivityRepository, ISocialRepository, IBoardQueries, IPlayerDataEraser, IUnitOfWork, IOutbox
{
    public List<Player> Players { get; } = [];

    public List<XpEntry> Ledger { get; } = [];

    public List<BadgeAward> Awards { get; } = [];

    public List<ActiveDay> Days { get; } = [];

    public List<DailyProgress> Daily { get; } = [];

    public List<Cheer> Cheers { get; } = [];

    public List<Challenge> Challenges { get; } = [];

    public List<IntegrationEvent> Events { get; } = [];

    public int Saves { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public void Add(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);

    public Task<Player?> GetAsync(Guid learnerId, CancellationToken cancellationToken) => Task.FromResult(Players.FirstOrDefault(p => p.Id == learnerId));

    public Task<IReadOnlyDictionary<Guid, Player>> GetManyAsync(IReadOnlyCollection<Guid> learnerIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Player>>(Players.Where(p => learnerIds.Contains(p.Id)).ToDictionary(p => p.Id));

    public Task<IReadOnlyList<Guid>> ListBatchAsync(string batch, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(Players.Where(p => p.Batch == batch).Select(p => p.Id).ToList());

    public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(Players.Count);

    public void Add(Player player) => Players.Add(player);

    public Task<bool> ExistsAsync(Guid learnerId, string reason, string sourceId, CancellationToken cancellationToken) =>
        Task.FromResult(Ledger.Any(e => e.LearnerId == learnerId && e.Reason == reason && e.SourceId == sourceId));

    public Task<DateTime?> LastCreditAsync(Guid learnerId, string reason, CancellationToken cancellationToken) =>
        Task.FromResult(Ledger.Where(e => e.LearnerId == learnerId && e.Reason == reason).Select(e => (DateTime?)e.OccurredAt).Max());

    public Task<int> TotalAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Ledger.Where(e => e.LearnerId == learnerId).Sum(e => e.Xp));

    public Task<PagedResult<XpEntry>> ListAsync(Guid learnerId, PageRequest page, CancellationToken cancellationToken)
    {
        var mine = Ledger.Where(e => e.LearnerId == learnerId).OrderByDescending(e => e.OccurredAt).ToList();
        return Task.FromResult(new PagedResult<XpEntry>(mine.Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, mine.Count));
    }

    public void Add(XpEntry entry) => Ledger.Add(entry);

    Task<IReadOnlyDictionary<string, DateTime>> IBadgeAwardRepository.ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, DateTime>>(Awards.Where(a => a.LearnerId == learnerId).ToDictionary(a => a.BadgeId, a => a.EarnedAt));

    public Task<IReadOnlyDictionary<string, int>> OwnershipAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, int>>(Awards.GroupBy(a => a.BadgeId).ToDictionary(g => g.Key, g => g.Count()));

    public void Add(BadgeAward award) => Awards.Add(award);

    public Task<IReadOnlyList<DateOnly>> ListDaysAsync(Guid learnerId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DateOnly>>(Days.Where(d => d.LearnerId == learnerId && d.Day >= from && d.Day <= to).Select(d => d.Day).ToList());

    public void Add(ActiveDay day) => Days.Add(day);

    public Task<IReadOnlyList<DailyProgress>> ListDailyAsync(Guid learnerId, DateOnly day, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DailyProgress>>(Daily.Where(d => d.LearnerId == learnerId && d.Day == day).ToList());

    public void Add(DailyProgress progress) => Daily.Add(progress);

    public Task<Cheer?> FindCheerAsync(Guid from, Guid to, CancellationToken cancellationToken) =>
        Task.FromResult(Cheers.FirstOrDefault(c => c.FromLearnerId == from && c.ToLearnerId == to));

    public Task<IReadOnlyList<Guid>> CheeredByAsync(Guid from, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(Cheers.Where(c => c.FromLearnerId == from && c.Active).Select(c => c.ToLearnerId).ToList());

    public Task<bool> OpenChallengeExistsAsync(Guid from, Guid to, DateTime now, CancellationToken cancellationToken) =>
        Task.FromResult(Challenges.Any(c => c.FromLearnerId == from && c.ToLearnerId == to && c.EndsAt > now));

    public void Add(Cheer cheer) => Cheers.Add(cheer);

    public void Add(Challenge challenge) => Challenges.Add(challenge);

    public Task<IReadOnlyList<BoardRow>> TopAsync(DateTime? since, DateTime? before, IReadOnlyCollection<Guid>? only, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BoardRow>>(Board(since, before, only).Take(limit).ToList());

    public Task<(BoardRow Row, int Rank)?> RankOfAsync(Guid learnerId, DateTime? since, IReadOnlyCollection<Guid>? only, CancellationToken cancellationToken)
    {
        var board = Board(since, null, only);
        var index = board.FindIndex(r => r.LearnerId == learnerId);
        return Task.FromResult<(BoardRow, int)?>(index < 0 ? null : (board[index], index + 1));
    }

    public Task<int> CountAsync(DateTime? since, IReadOnlyCollection<Guid>? only, CancellationToken cancellationToken) =>
        Task.FromResult(Board(since, null, only).Count);

    public Task EraseAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        Players.RemoveAll(p => p.Id == learnerId);
        Ledger.RemoveAll(e => e.LearnerId == learnerId);
        Awards.RemoveAll(a => a.LearnerId == learnerId);
        Days.RemoveAll(d => d.LearnerId == learnerId);
        Daily.RemoveAll(d => d.LearnerId == learnerId);
        Cheers.RemoveAll(c => c.FromLearnerId == learnerId || c.ToLearnerId == learnerId);
        Challenges.RemoveAll(c => c.FromLearnerId == learnerId || c.ToLearnerId == learnerId);
        return Task.CompletedTask;
    }

    private List<BoardRow> Board(DateTime? since, DateTime? before, IReadOnlyCollection<Guid>? only) =>
        Ledger.Where(e => (since is null || e.OccurredAt >= since) && (before is null || e.OccurredAt < before) && (only is null || only.Contains(e.LearnerId)))
            .GroupBy(e => e.LearnerId)
            .Select(g => new BoardRow(g.Key, g.Sum(e => e.Xp), g.Max(e => e.OccurredAt)))
            .OrderByDescending(r => r.Xp)
            .ThenBy(r => r.LastEarnedAt)
            .ToList();
}

/// <summary>The engine and use cases on the in-memory store, at a fixed time (Tuesday 13 Oct 2026, 09:00 UTC = 14:30 IST).</summary>
public sealed class GameHarness
{
    public GameHarness()
    {
        Options = MsOptions.Create(Settings);
    }

    public GamificationOptions Settings { get; } = new();

    public Microsoft.Extensions.Options.IOptions<GamificationOptions> Options { get; }

    public GameStore Store { get; } = new();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));

    public ITenantContext Tenant { get; } = new FixedTenant();

    /// <summary>A new engine per "event", like the dispatcher's scope.</summary>
    public GameEngine Engine() => new(Store, Store, Store, Store, Store, Tenant, Clock, Options, NullLogger<GameEngine>.Instance);

    public ArenaService Arena() => new(Store, Store, Store, Store, Engine(), Store, Tenant, Clock, Options);

    public StatsService Stats() => new(Store, Store, Store, Store, Store, Engine(), Store, Tenant, Clock, Options);

    public Player Player(Guid id) => Store.Players.Single(p => p.Id == id);

    public async Task<Guid> JoinAsync(string name)
    {
        var id = Guid.NewGuid();
        await new Gamification.Application.Handlers.AccountRegisteredHandler(Engine(), Clock)
            .HandleAsync(new AccountRegistered(id, name.Split(' ')[0], name.Contains(' ', StringComparison.Ordinal) ? name[(name.IndexOf(' ', StringComparison.Ordinal) + 1)..] : string.Empty, "IN") { TenantId = "skillmuni-in" }, CancellationToken.None);
        return id;
    }

    /// <summary>Credits <paramref name="xp"/> under a test reason, as one event would (badges and totals included).</summary>
    public async Task GiveXpAsync(Guid id, int xp)
    {
        var engine = Engine();
        var player = await engine.PlayerAsync(id, null, null, CancellationToken.None);
        await engine.AwardAsync(player, "test", Guid.NewGuid().ToString(), xp, CancellationToken.None);
        await engine.FinishAsync(player, CancellationToken.None);
    }

    private sealed class FixedTenant : ITenantContext
    {
        public string TenantId => "skillmuni-in";

        public string Market => "IN";
    }
}
