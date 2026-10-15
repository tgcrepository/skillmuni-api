using Learning.Application;
using Learning.Application.Services;
using Learning.Domain;
using Learning.Infrastructure.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Tenancy;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Learning.UnitTests;

/// <summary>Every repository in memory. Adds are visible at once (no separate save step).</summary>
public sealed class LearnStore : IBriefProgressRepository, IQuizRepository, ILobbyRepository, IActivityRepository, ILearnerDataEraser, IUnitOfWork, IOutbox
{
    public List<BriefProgress> Progress { get; } = [];

    public List<QuizDraft> Drafts { get; } = [];

    public List<QuizAttempt> Attempts { get; } = [];

    public List<LobbyState> Lobby { get; } = [];

    public List<ActivityDay> Activity { get; } = [];

    public List<IntegrationEvent> Events { get; } = [];

    public int Saves { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public void Add(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);

    public IReadOnlyList<T> Published<T>() => Events.OfType<T>().ToList();

    public Task<IReadOnlyList<BriefProgress>> ListModuleAsync(Guid learnerId, string moduleSlug, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BriefProgress>>(Progress.Where(p => p.LearnerId == learnerId && p.ModuleSlug == moduleSlug).ToList());

    public Task<IReadOnlyList<BriefProgress>> ListModuleForUpdateAsync(Guid learnerId, string moduleSlug, CancellationToken cancellationToken) =>
        ListModuleAsync(learnerId, moduleSlug, cancellationToken);

    public Task<BriefProgress?> GetAsync(Guid learnerId, string briefId, CancellationToken cancellationToken) =>
        Task.FromResult(Progress.FirstOrDefault(p => p.LearnerId == learnerId && p.BriefId == briefId));

    public Task<IReadOnlyList<PassedBrief>> ListPassedAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PassedBrief>>(Progress.Where(p => p.LearnerId == learnerId && p.Passed).Select(ModuleProgress.ToPassed).ToList());

    public Task<bool> AnyPassedAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Progress.Any(p => p.LearnerId == learnerId && p.Passed));

    public void Add(BriefProgress progress) => Progress.Add(progress);

    public Task<QuizDraft?> GetDraftAsync(Guid learnerId, string quizId, CancellationToken cancellationToken) =>
        Task.FromResult(Drafts.FirstOrDefault(d => d.LearnerId == learnerId && d.QuizId == quizId));

    public void Add(QuizDraft draft) => Drafts.Add(draft);

    public void Remove(QuizDraft draft) => Drafts.Remove(draft);

    public void Add(QuizAttempt attempt) => Attempts.Add(attempt);

    public Task<LobbyState?> GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Lobby.FirstOrDefault(s => s.LearnerId == learnerId));

    public void Add(LobbyState state) => Lobby.Add(state);

    public Task<ActivityDay?> GetAsync(Guid learnerId, DateOnly day, CancellationToken cancellationToken) =>
        Task.FromResult(Activity.FirstOrDefault(d => d.LearnerId == learnerId && d.Day == day));

    public void Add(ActivityDay day) => Activity.Add(day);

    public Task EraseAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        Progress.RemoveAll(p => p.LearnerId == learnerId);
        Drafts.RemoveAll(p => p.LearnerId == learnerId);
        Attempts.RemoveAll(p => p.LearnerId == learnerId);
        Lobby.RemoveAll(p => p.LearnerId == learnerId);
        Activity.RemoveAll(p => p.LearnerId == learnerId);
        return Task.CompletedTask;
    }
}

public sealed class FixedTenant(string market = "IN") : ITenantContext
{
    public string TenantId => "skillmuni-in";

    public string Market => market;
}

/// <summary>The services wired to one in-memory store, the seed catalogue and a fake clock (15 Oct 2026, 10:00 IST).</summary>
public sealed class Harness
{
    public Harness()
    {
        var options = MsOptions.Create(new LearningOptions());
        Learn = new LearnService(Content, Store, Store, Store, Store, Store, Tenant, Clock, options, NullLogger<LearnService>.Instance);
        Home = new HomeService(Content, Store, Store, Store, Store, Tenant, Clock, options, NullLogger<HomeService>.Instance);
        Stats = new LearningStatsService(Content, Store, options);
    }

    public static SeedLearningContent Content { get; } = new();

    public LearnStore Store { get; } = new();

    public FixedTenant Tenant { get; } = new();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 15, 4, 30, 0, TimeSpan.Zero));

    public Guid LearnerId { get; } = Guid.NewGuid();

    public LearnService Learn { get; }

    public HomeService Home { get; }

    public LearningStatsService Stats { get; }

    /// <summary>Answers every question of a brief's quiz, right or wrong.</summary>
    public static Dictionary<string, string> Answers(string module, string topic, int brief, bool right)
    {
        var questions = Content.Module(module)!.Topic(topic)!.QuestionsFor(brief);
        return questions.ToDictionary(q => q.Id, q => right ? q.CorrectOptionId : q.OptionId(q.Answer == 0 ? 1 : 0));
    }

    /// <summary>Submits the current brief's quiz of a topic with every answer right.</summary>
    public async Task<int> PassNextAsync(string module, string topic)
    {
        var quiz = (await Learn.QuizAsync(LearnerId, module, topic, CancellationToken.None)).Value;
        var index = BriefKey.TryParse(quiz.Id, out var key) ? key.Index : -1;
        var result = await Learn.SubmitAsync(LearnerId, quiz.Id, Answers(module, topic, index, right: true), CancellationToken.None);
        Clock.Advance(TimeSpan.FromMinutes(1));
        return result.Value.Xp;
    }
}
