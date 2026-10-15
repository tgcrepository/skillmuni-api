using Assessment.Application;
using Assessment.Application.Services;
using Assessment.Domain;
using Assessment.Infrastructure.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Tenancy;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Assessment.UnitTests;

/// <summary>Every repository in memory. Adds are visible at once (no separate save step).</summary>
public sealed class AssessmentStore : ICareerJourneyRepository, ICareerAttemptRepository, IAssessmentDataEraser, IUnitOfWork, IOutbox
{
    public List<CareerJourney> Journeys { get; } = [];

    public List<CareerAttempt> Attempts { get; } = [];

    public List<IntegrationEvent> Events { get; } = [];

    public int Saves { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public void Add(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);

    public Task<CareerJourney?> GetAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult(Journeys.FirstOrDefault(j => j.Id == learnerId));

    public Task<bool> ExistsAsync(Guid learnerId, CancellationToken cancellationToken) => Task.FromResult(Journeys.Any(j => j.Id == learnerId));

    public void Add(CareerJourney journey) => Journeys.Add(journey);

    public Task<IReadOnlyList<CareerAttempt>> ListAsync(Guid learnerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CareerAttempt>>(Attempts.Where(a => a.LearnerId == learnerId).OrderBy(a => a.Number).ToList());

    public void Add(CareerAttempt attempt) => Attempts.Add(attempt);

    public Task EraseAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        Journeys.RemoveAll(j => j.Id == learnerId);
        Attempts.RemoveAll(a => a.LearnerId == learnerId);
        return Task.CompletedTask;
    }
}

public sealed class FixedTenant : ITenantContext
{
    public string TenantId => "skillmuni-in";

    public string Market => "IN";
}

/// <summary>The real survey content, the service and an in-memory store.</summary>
public sealed class AssessmentHarness
{
    public AssessmentHarness()
    {
        Service = new CareerAssessmentService(
            Content,
            Store,
            Store,
            Store,
            Store,
            new FixedTenant(),
            Clock,
            MsOptions.Create(Options),
            NullLogger<CareerAssessmentService>.Instance);
    }

    public static ICareerDriversContentProvider Content { get; } = new EmbeddedCareerDriversContent();

    public static int PairCount => Content.Content.Pairs.Count;

    public AssessmentStore Store { get; } = new();

    /// <summary>15 Oct 2026, 20:00 UTC = 16 Oct in India.</summary>
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 15, 20, 0, 0, TimeSpan.Zero));

    public AssessmentOptions Options { get; } = new();

    public CareerAssessmentService Service { get; }

    public Guid LearnerId { get; } = Guid.NewGuid();

    /// <summary>The first <paramref name="count"/> pairs answered with <paramref name="notch"/>, the rest not reached.</summary>
    public static List<Answer> Answered(int count, int notch = 1) =>
        Enumerable.Range(0, PairCount).Select(i => i < count ? Answer.FromNotch(notch) : Answer.None).ToList();

    public static List<Answer> All(int notch = 1) => Answered(PairCount, notch);

    public Task<SaveAnswersResponse> SaveAsync(IReadOnlyList<Answer> answers, int current = 0) =>
        Service.SaveAsync(LearnerId, new SaveAnswersRequest(answers, current), CancellationToken.None);

    public Task<SkillMuni.Common.Results.Result<AssessmentResultDto>> SubmitAsync(IReadOnlyList<Answer> answers) =>
        Service.SubmitAsync(LearnerId, new SubmitAnswersRequest(answers), CancellationToken.None);

    public IReadOnlyList<T> EventsOf<T>()
        where T : IntegrationEvent => Store.Events.OfType<T>().ToList();
}
