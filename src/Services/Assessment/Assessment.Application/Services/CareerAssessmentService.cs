using Assessment.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Assessment.Application.Services;

/// <summary>API-004–010: Career Drivers ("Muni's Balance"), behaving like <c>src/mocks/assessment.handlers.ts</c>.</summary>
public interface ICareerAssessmentService
{
    Task<AssessmentStateDto> StateAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<SaveAnswersResponse> SaveAsync(Guid learnerId, SaveAnswersRequest request, CancellationToken cancellationToken);

    Task<Result<AssessmentResultDto>> SubmitAsync(Guid learnerId, SubmitAnswersRequest request, CancellationToken cancellationToken);

    /// <param name="attempt">1-based; null or unknown = the latest attempt.</param>
    Task<Result<AssessmentResultDto>> ResultAsync(Guid learnerId, int? attempt, CancellationToken cancellationToken);

    Task<AssessmentStateDto> RestartAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<AssessmentStateDto> RetakeAsync(Guid learnerId, CancellationToken cancellationToken);

    Task<RemindResponse> SetRemindAsync(Guid learnerId, bool remind, CancellationToken cancellationToken);
}

public sealed partial class CareerAssessmentService(
    ICareerDriversContentProvider contentProvider,
    ICareerJourneyRepository journeys,
    ICareerAttemptRepository attempts,
    IUnitOfWork unitOfWork,
    IOutbox outbox,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<AssessmentOptions> options,
    ILogger<CareerAssessmentService> logger) : ICareerAssessmentService
{
    public const int TopDriverCount = 3;

    private CareerDriversContent Content => contentProvider.Content;

    private AssessmentOptions Options => options.Value;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<AssessmentStateDto> StateAsync(Guid learnerId, CancellationToken cancellationToken) =>
        View(await journeys.GetAsync(learnerId, cancellationToken));

    public async Task<SaveAnswersResponse> SaveAsync(Guid learnerId, SaveAnswersRequest request, CancellationToken cancellationToken)
    {
        var journey = await JourneyAsync(learnerId, cancellationToken);
        var before = journey.Xp;
        var cleared = journey.Save(request.Answers ?? [], request.Current ?? 0, Content, Options.XpPerIsland, Now);
        PublishCleared(journey, cleared, journey.EarnsXp);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SaveAnswersResponse(journey.Xp, journey.Xp - before);
    }

    public async Task<Result<AssessmentResultDto>> SubmitAsync(Guid learnerId, SubmitAnswersRequest request, CancellationToken cancellationToken)
    {
        var answers = request.Answers ?? [];
        if (answers.Count != Content.Pairs.Count || !answers.All(a => a.IsAnswered))
        {
            return AssessmentErrors.Incomplete;
        }

        var journey = await JourneyAsync(learnerId, cancellationToken);
        var history = (await attempts.ListAsync(learnerId, cancellationToken)).ToList();
        var number = Math.Max(journey.Attempts, history.Count == 0 ? 0 : history.Max(a => a.Number)) + 1;
        var now = Now;

        var (cleared, attemptXp, firstAttempt) = journey.Submit(answers, Content, Options.XpPerIsland, Options.XpOnSubmit, now);
        PublishCleared(learnerId, number, cleared, firstAttempt);

        var scores = CareerDriversScoring.Score(Content.Pairs, answers);
        var archetype = CareerDriversScoring.Match(scores, Content.Archetypes);
        var ranking = CareerDriversScoring.Rank(scores);
        var takenOn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, Options.TimeZoneFor(tenant.Market)));
        var attempt = CareerAttempt.Record(learnerId, tenant.TenantId, number, takenOn, scores, archetype.Id, attemptXp, answers, now);
        attempts.Add(attempt);
        history.Add(attempt);

        var top = ranking.Take(TopDriverCount).ToList();
        outbox.Add(new AttemptSubmitted(
            learnerId,
            number,
            firstAttempt,
            archetype.Id,
            archetype.Name,
            archetype.Emoji,
            archetype.Summary,
            top.Select(d => Content.FindDriver(d)?.Name ?? d).ToList(),
            top)
        {
            TenantId = tenant.TenantId,
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogSubmitted(logger, learnerId, number, archetype.Id, attemptXp);
        return Build(history, attempt);
    }

    public async Task<Result<AssessmentResultDto>> ResultAsync(Guid learnerId, int? attempt, CancellationToken cancellationToken)
    {
        var history = await attempts.ListAsync(learnerId, cancellationToken);
        if (history.Count == 0)
        {
            return AssessmentErrors.NoResult;
        }

        var chosen = history.FirstOrDefault(a => a.Number == attempt) ?? history.MaxBy(a => a.Number)!;
        return Build(history, chosen);
    }

    public async Task<AssessmentStateDto> RestartAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var journey = await JourneyAsync(learnerId, cancellationToken);
        journey.Restart(Now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return View(journey);
    }

    public async Task<AssessmentStateDto> RetakeAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var journey = await JourneyAsync(learnerId, cancellationToken);
        journey.Retake(Now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return View(journey);
    }

    public async Task<RemindResponse> SetRemindAsync(Guid learnerId, bool remind, CancellationToken cancellationToken)
    {
        // Sending the 6 PM nudge belongs to the Platform notifications service (A-218); this stores the choice.
        var journey = await JourneyAsync(learnerId, cancellationToken);
        journey.SetRemind(remind, Now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new RemindResponse(journey.Remind);
    }

    private async Task<CareerJourney> JourneyAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        if (await journeys.GetAsync(learnerId, cancellationToken) is { } journey)
        {
            return journey;
        }

        // Lazy start: a request can arrive before identity.learner.registered is handled.
        journey = CareerJourney.Start(learnerId, tenant.TenantId, Now);
        journeys.Add(journey);
        return journey;
    }

    private void PublishCleared(CareerJourney journey, IReadOnlyList<int> islands, bool firstAttempt) =>
        PublishCleared(journey.LearnerId, journey.AttemptInProgress, islands, firstAttempt);

    private void PublishCleared(Guid learnerId, int attempt, IReadOnlyList<int> islands, bool firstAttempt)
    {
        foreach (var island in islands)
        {
            outbox.Add(new IslandCleared(learnerId, attempt, island, firstAttempt) { TenantId = tenant.TenantId });
        }
    }

    private AssessmentStateDto View(CareerJourney? journey)
    {
        var answers = journey?.Answers(Content.Pairs.Count) ?? Answer.Empty(Content.Pairs.Count);
        var submitted = journey?.Attempts ?? 0;
        var status = answers.Any(a => !a.IsNone) ? "in-progress" : submitted > 0 ? "done" : "new";
        return new AssessmentStateDto(
            status,
            Content.Pairs.Select(p => p.ToDto()).ToList(),
            Content.Islands.Select(i => i.ToDto()).ToList(),
            answers,
            journey?.Current ?? 0,
            journey?.Xp ?? 0,
            journey?.EarnsXp ?? true,
            journey?.Remind ?? true,
            submitted);
    }

    private AssessmentResultDto Build(IReadOnlyList<CareerAttempt> history, CareerAttempt chosen)
    {
        var previous = history.FirstOrDefault(a => a.Number == chosen.Number - 1);
        return new AssessmentResultDto(
            chosen.Number,
            chosen.TakenOn.ToIsoDate(),
            chosen.Scores.ToDictionary(),
            CareerDriversScoring.Rank(chosen.Scores),
            ArchetypeOf(chosen).ToDto(),
            Content.Drivers.Select(d => d.ToDto()).ToList(),
            previous?.Scores.ToDictionary(),
            history.OrderByDescending(a => a.Number)
                .Select(a => new AttemptRefDto(a.Number, a.TakenOn.ToIsoDate(), ArchetypeOf(a).Name))
                .ToList(),
            chosen.XpEarned,
            Options.MandatoryCourses);
    }

    /// <summary>The archetype matched at submit time; re-matched if the content no longer has it.</summary>
    private Archetype ArchetypeOf(CareerAttempt attempt) =>
        Content.FindArchetype(attempt.ArchetypeId) ?? CareerDriversScoring.Match(attempt.Scores, Content.Archetypes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Career Drivers submitted {LearnerId} {Attempt} {Archetype} {Xp}")]
    private static partial void LogSubmitted(ILogger logger, Guid learnerId, int attempt, string archetype, int xp);
}
