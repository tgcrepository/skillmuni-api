using Learning.Application.Contracts;
using Learning.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Learning.Application.Services;

/// <summary>API-061…063: the Lobby feed and the "Your first moves" card (A-233, A-266, BR-42, BR-107).</summary>
public interface IHomeService
{
    Task<HomeFeedDto> FeedAsync(Guid learnerId, CancellationToken cancellationToken);

    /// <summary>A first move done by visiting it. Only "jobs" is recorded; the others complete by doing them.</summary>
    Task<Result> CompleteStepAsync(Guid learnerId, string stepId, CancellationToken cancellationToken);

    /// <summary>Collects the +50 bonus once all three moves are done; 0 when it was already collected.</summary>
    Task<Result<FirstMovesClaimDto>> ClaimFirstMovesAsync(Guid learnerId, CancellationToken cancellationToken);
}

public sealed partial class HomeService(
    ILearningContent content,
    IBriefProgressRepository progress,
    ILobbyRepository lobby,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<LearningOptions> options,
    ILogger<HomeService> logger) : IHomeService
{
    public const string AssessmentStep = "assessment";
    public const string LearnStep = "learn";
    public const string JobsStep = "jobs";

    public async Task<HomeFeedDto> FeedAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var passed = await progress.ListPassedAsync(learnerId, cancellationToken);
        var state = await lobby.GetAsync(learnerId, cancellationToken);

        // Once the bonus is collected the card has done its job and makes way for the usual Lobby (A-266).
        var claimed = state?.Claimed ?? false;
        return new HomeFeedDto(
            Daypart(),
            Resume(passed),
            claimed ? null : Steps(state, passed.Count > 0),
            claimed ? null : new FirstMovesBonusDto(options.Value.FirstMovesXp, false),
            content.Pathways.Select(p => new PathwayDto(p.Slug, p.Title, p.Blurb, p.Tone, p.Art, p.Link)).ToList(),
            content.Knooks.Select(k => new KnookDto(
                k.Slug,
                k.Title,
                k.Icon,
                k.Expanded,
                k.Modules.Select(m => new KnookModuleDto(m.Slug, m.Title, m.Art, m.Link)).ToList())).ToList(),
            RewardsReady: 0,
            UnreadNotifications: 0);
    }

    public async Task<Result> CompleteStepAsync(Guid learnerId, string stepId, CancellationToken cancellationToken)
    {
        switch (stepId)
        {
            case JobsStep:
                var state = await GetOrStartAsync(learnerId, cancellationToken);
                state.MarkJobsVisited(clock.GetUtcNow().UtcDateTime);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success();
            case AssessmentStep or LearnStep:
                return Result.Success();
            default:
                return LearningErrors.StepNotFound;
        }
    }

    public async Task<Result<FirstMovesClaimDto>> ClaimFirstMovesAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var state = await GetOrStartAsync(learnerId, cancellationToken);
        if (state.Claimed)
        {
            return new FirstMovesClaimDto(0);
        }

        var learned = await progress.AnyPassedAsync(learnerId, cancellationToken);
        if (state.AssessmentDoneAt is null || state.JobsVisitedAt is null || !learned)
        {
            return LearningErrors.FirstMovesNotDone;
        }

        state.Claim(clock.GetUtcNow().UtcDateTime);
        outbox.Add(new FirstMovesClaimed(learnerId) { TenantId = tenant.TenantId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogClaimed(logger, learnerId);
        return new FirstMovesClaimDto(options.Value.FirstMovesXp);
    }

    /// <summary>Greeting from the learner's local time (market time zone, A-170).</summary>
    internal string Daypart()
    {
        var hour = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), options.Value.TimeZoneFor(tenant.Market)).Hour;
        return hour < 12 ? "morning" : hour < 17 ? "afternoon" : "evening";
    }

    private List<FirstStepDto> Steps(LobbyState? state, bool learned) =>
    [
        new(AssessmentStep, "Discover your Career Drivers", "36 quick this-or-that picks · 6 min", options.Value.CareerDriversXp, "/assessments/career", state?.AssessmentDoneAt is not null),
        new(LearnStep, "Start your first learning module", "A 2-minute brief, then a quick quiz · XP for every right answer", null, Links.Module("brain-vita"), learned),
        new(JobsStep, "See jobs that fit you", "Matched to what you told us in your quests", null, "/jobs/matching", state?.JobsVisitedAt is not null),
    ];

    /// <summary>"Pick up where you left off": the topic of the last checked quiz, or the module's next unfinished one.</summary>
    private ResumeCardDto? Resume(IReadOnlyList<PassedBrief> passed)
    {
        var last = passed.OrderByDescending(p => p.LastSubmittedAt).FirstOrDefault();
        if (last is null || content.Module(last.ModuleSlug) is not { } module)
        {
            return null;
        }

        var state = new ModuleProgress(module, passed);
        if (state.Resume(last.TopicSlug) is not { } resume)
        {
            return null;
        }

        var (topic, index) = resume;
        var done = state.Done(topic);
        var left = topic.Briefs.Count - done;
        return new ResumeCardDto(
            module.Title,
            topic.Title,
            index + 1,
            topic.Briefs.Count,
            left == 1 ? "1 quiz away from 100% mastery" : $"{left} quizzes away from 100% mastery",
            done * 100 / topic.Briefs.Count,
            topic.QuestionsFor(index).Count,
            module.TileArt,
            Links.Topic(module.Slug, topic.Slug));
    }

    // Insert-if-absent: assessment.attempt.submitted may be creating the same row right now.
    private async Task<LobbyState> GetOrStartAsync(Guid learnerId, CancellationToken cancellationToken) =>
        await lobby.GetAsync(learnerId, cancellationToken)
        ?? await lobby.GetOrCreateAsync(LobbyState.Start(tenant.TenantId, learnerId, clock.GetUtcNow().UtcDateTime), cancellationToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "First-moves bonus collected {LearnerId}")]
    private static partial void LogClaimed(ILogger logger, Guid learnerId);
}
