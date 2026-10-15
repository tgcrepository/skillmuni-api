using Microsoft.Extensions.Time.Testing;
using Muni.Application;
using Muni.Application.Responders;
using Muni.Application.Services;
using Muni.Domain;
using SkillMuni.Common.Tenancy;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Muni.UnitTests;

/// <summary>Every repository and client in memory.</summary>
public sealed class MuniStore : IMuniLearnerRepository, IChatRepository, IRatingRepository, IUnitOfWork, IProfileProgressClient, IPrivateFieldClient, ITenantContext
{
    public List<MuniLearner> Learners { get; } = [];

    public List<ChatMessage> Messages { get; } = [];

    public List<ReplyRating> Ratings { get; } = [];

    public int Saves { get; private set; }

    public ProfileProgress? Progress { get; set; } = new(50, [new ProfileQuest(4, "your-flex-era", "Your Flex Era", [new ProfileStep("Languages", 3), new ProfileStep("Hobbies", 4)])]);

    public Dictionary<string, string?> Private { get; } = new()
    {
        [RevealFields.Email] = "yash.pohare@thegamification.com",
        [RevealFields.Mobile] = "+91 98765 40697",
    };

    public string TenantId => "skillmuni-in";

    public string Market => "IN";

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public Task<MuniLearner?> GetAsync(Guid learnerId, CancellationToken cancellationToken) => Task.FromResult(Learners.FirstOrDefault(l => l.Id == learnerId));

    public Task<MuniLearner?> FindAsync(Guid learnerId, CancellationToken cancellationToken) => GetAsync(learnerId, cancellationToken);

    public void Add(MuniLearner learner) => Learners.Add(learner);

    public Task<ChatLine?> LastAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Messages.Where(m => m.LearnerId == learnerId).OrderByDescending(m => m.CreatedAt).Select(Line).FirstOrDefault());

    public Task<IReadOnlyList<ChatLine>> RecentAsync(Guid learnerId, DateTime since, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ChatLine>>(Messages.Where(m => m.LearnerId == learnerId && m.CreatedAt >= since).OrderByDescending(m => m.CreatedAt).Take(limit).Select(Line).ToList());

    public void Add(ChatMessage message) => Messages.Add(message);

    public Task<ReplyRating?> FindAsync(Guid learnerId, string messageRef, CancellationToken cancellationToken) =>
        Task.FromResult(Ratings.FirstOrDefault(r => r.LearnerId == learnerId && r.MessageRef == messageRef));

    public void Add(ReplyRating rating) => Ratings.Add(rating);

    public void Remove(ReplyRating rating) => Ratings.Remove(rating);

    Task<ProfileProgress?> IProfileProgressClient.GetAsync(Guid learnerId, CancellationToken cancellationToken) => Task.FromResult(Progress);

    public Task<string?> RevealAsync(string field, CancellationToken cancellationToken) => Task.FromResult(Private.GetValueOrDefault(field));

    private static ChatLine Line(ChatMessage m) => new(m.Id, m.ConversationId, m.From, m.Text, m.CreatedAt);
}

public sealed class MuniHarness
{
    public MuniHarness()
    {
        var options = MsOptions.Create(Options);
        Snapshots = new LearnerSnapshotBuilder(Store, Store, options);
        Responder = new KeywordMuniResponder(Store, options);
        Service = new MuniService(Snapshots, Responder, Store, Store, Store, Store, Store, Clock, options);
    }

    public static readonly Guid LearnerId = Guid.Parse("7b0b1f8e-6a41-4c55-9d62-1d1c2a9b0e11");

    public MuniOptions Options { get; } = new();

    public MuniStore Store { get; } = new();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 15, 6, 0, 0, TimeSpan.Zero));

    public LearnerSnapshotBuilder Snapshots { get; }

    public KeywordMuniResponder Responder { get; }

    public MuniService Service { get; }

    public LearnerRef Who { get; } = new(LearnerId, "Yash", "IN");

    /// <summary>A learner the events already told Muni about.</summary>
    public MuniLearner Known(int xp = 120, int streak = 2, string? archetype = "The Connector")
    {
        var learner = MuniLearner.Create("skillmuni-in", LearnerId, 1, "Rookie", 400, Clock.GetUtcNow().UtcDateTime);
        learner.Register("Yash", "Pohare", new DateTime(2025, 4, 3, 0, 0, 0, DateTimeKind.Utc), Clock.GetUtcNow().UtcDateTime);
        learner.ApplyTotals(xp, 1, "Rookie", 400, streak, Clock.GetUtcNow().UtcDateTime);
        learner.SetArchetype(archetype, Clock.GetUtcNow().UtcDateTime);
        Store.Learners.Add(learner);
        return learner;
    }

    public async Task<LearnerSnapshot> MeAsync() => await Snapshots.BuildAsync(Who, CancellationToken.None);
}
