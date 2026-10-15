using Gamification.Application.Rules;
using Gamification.Application.Services;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;

namespace Gamification.Application.Handlers;

// One handler per event. Each credits XP through the engine (once per reason + source, BRD §4.1), moves counters, and
// calls FinishAsync to award badges and queue the totals event. The dispatcher saves everything with the inbox row.
// Ledger keys follow docs/EVENTS.md; an action not in §4.1 earns nothing.

public sealed class AccountRegisteredHandler(IGameEngine engine, TimeProvider clock) : IIntegrationEventHandler<AccountRegistered>
{
    public async Task HandleAsync(AccountRegistered e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.AccountId, $"{e.FirstName} {e.LastName}".Trim(), e.Market, cancellationToken);
        player.Rename($"{e.FirstName} {e.LastName}".Trim(), clock.GetUtcNow().UtcDateTime);
        await engine.FinishAsync(player, cancellationToken);
    }
}

public sealed class AccountErasedHandler(IPlayerDataEraser eraser) : IIntegrationEventHandler<AccountErased>
{
    public Task HandleAsync(AccountErased e, CancellationToken cancellationToken) => eraser.EraseAsync(e.AccountId, cancellationToken);
}

/// <summary>
/// BR-100 / BR-101: +20 once per step when Learner marks it paid. An unpaid step (an optional one finished with
/// nothing added) still counts towards the quest badge's progress and can't be paid later. Claimed learners get the
/// same events from Learner (BR-119).
/// </summary>
public sealed class QuestStepHandler(IGameEngine engine, TimeProvider clock, IOptions<GamificationOptions> options) : IIntegrationEventHandler<QuestStepCompleted>
{
    public async Task HandleAsync(QuestStepCompleted e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        player.CountAtLeast(Counters.QuestParts(e.Slug), e.Part, clock.GetUtcNow().UtcDateTime);
        if (e.Paid)
        {
            await engine.AwardAsync(player, "quest.step", $"{e.Slug}:{e.Part}", options.Value.Xp.QuestStep, cancellationToken);
        }

        await engine.FinishAsync(player, cancellationToken);
    }
}

/// <summary>BR-102: +50 once per quest, and its badge (§4.3).</summary>
public sealed class QuestCompletedHandler(IGameEngine engine, TimeProvider clock, IOptions<GamificationOptions> options) : IIntegrationEventHandler<QuestCompleted>
{
    public async Task HandleAsync(QuestCompleted e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        if (await engine.AwardAsync(player, "quest.complete", e.Slug, options.Value.Xp.QuestComplete, cancellationToken) > 0)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            player.CountAtLeast(Counters.QuestDone(e.Slug), 1, now);
            player.Count(Counters.QuestsCompleted, 1, now);
        }

        await engine.FinishAsync(player, cancellationToken);
    }
}

/// <summary>BR-108 photo +5, BR-109 top skills +10, once each.</summary>
public sealed class ProfileTaskHandler(IGameEngine engine, IOptions<GamificationOptions> options) : IIntegrationEventHandler<ProfileTaskCompleted>
{
    public async Task HandleAsync(ProfileTaskCompleted e, CancellationToken cancellationToken)
    {
        var xp = e.Task switch
        {
            "photo" => options.Value.Xp.ProfilePhoto,
            "skills" => options.Value.Xp.ProfileSkills,
            _ => 0,
        };
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        await engine.AwardAsync(player, "profile.task", e.Task, xp, cancellationToken);
        await engine.FinishAsync(player, cancellationToken);
    }
}

/// <summary>BR-110: the Launchpad mission "Finish job profile", +50 once.</summary>
public sealed class JobProfileCompletedHandler(IGameEngine engine, IOptions<GamificationOptions> options) : IIntegrationEventHandler<JobProfileCompleted>
{
    public Task HandleAsync(JobProfileCompleted e, CancellationToken cancellationToken) =>
        Once.AwardAsync(engine, e.LearnerId, "mission", "job-profile", options.Value.Xp.JobProfileComplete, cancellationToken);
}

/// <summary>BR-113: +100 once per learner, whatever the code; the programme becomes the learner's Arena batch.</summary>
public sealed class ProgrammeEnrolledHandler(IGameEngine engine, TimeProvider clock, IOptions<GamificationOptions> options) : IIntegrationEventHandler<ProgrammeEnrolled>
{
    public async Task HandleAsync(ProgrammeEnrolled e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        player.JoinBatch(e.ProgrammeCode, clock.GetUtcNow().UtcDateTime);
        await engine.AwardAsync(player, "programme.enrolled", "once", options.Value.Xp.Programme, cancellationToken);
        await engine.FinishAsync(player, cancellationToken);
    }
}

/// <summary>
/// BR-114 / A-274: +5 at most once every 30 days, decided here from the ledger (the last <c>feedback</c> credit),
/// measured at the event's time. Each paid feedback is keyed by its event id.
/// </summary>
public sealed class FeedbackSentHandler(IGameEngine engine, IXpLedger ledger, TimeProvider clock, IOptions<GamificationOptions> options) : IIntegrationEventHandler<FeedbackSent>
{
    public const string Reason = "feedback";

    public async Task HandleAsync(FeedbackSent e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        var sentAt = e.OccurredAt == default ? clock.GetUtcNow().UtcDateTime : e.OccurredAt;
        var last = await ledger.LastCreditAsync(e.LearnerId, Reason, cancellationToken);
        if (last is null || sentAt - last.Value >= options.Value.FeedbackXpEvery)
        {
            await engine.AwardAsync(player, Reason, e.EventId.ToString(), options.Value.Xp.Feedback, cancellationToken);
        }

        await engine.FinishAsync(player, cancellationToken);
    }
}

public sealed class PhotoChangedHandler(IGameEngine engine, TimeProvider clock) : IIntegrationEventHandler<PhotoChanged>
{
    public async Task HandleAsync(PhotoChanged e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        player.SetPhoto(e.PhotoUrl, clock.GetUtcNow().UtcDateTime);
    }
}

/// <summary>BR-104: +25 per island 1–3, first attempt only. Island 4 is paid by the submit (BR-105).</summary>
public sealed class IslandClearedHandler(IGameEngine engine, IOptions<GamificationOptions> options) : IIntegrationEventHandler<IslandCleared>
{
    public const int PaidIslands = 3;

    public async Task HandleAsync(IslandCleared e, CancellationToken cancellationToken)
    {
        if (!e.FirstAttempt || e.Island is < 1 or > PaidIslands)
        {
            return;
        }

        await Once.AwardAsync(engine, e.LearnerId, "assessment", $"island:{e.Island}", options.Value.Xp.IslandCleared, cancellationToken);
    }
}

/// <summary>BR-105: Career Drivers submitted, +25 on the first attempt (100 in all with the islands). Career Clarity progress.</summary>
public sealed class AssessmentSubmittedHandler(IGameEngine engine, TimeProvider clock, IOptions<GamificationOptions> options) : IIntegrationEventHandler<AssessmentSubmitted>
{
    public async Task HandleAsync(AssessmentSubmitted e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        player.CountAtLeast(Counters.CareerDrivers, 1, clock.GetUtcNow().UtcDateTime);
        if (e.FirstAttempt)
        {
            await engine.AwardAsync(player, "assessment", "submitted", options.Value.Xp.AssessmentSubmitted, cancellationToken);
        }

        await engine.FinishAsync(player, cancellationToken);
    }
}

/// <summary>BR-118 "Learn for 15 minutes". Learning time doesn't keep the streak (B-45).</summary>
public sealed class LearningActivityHandler(IGameEngine engine, TimeProvider clock) : IIntegrationEventHandler<LearningActivity>
{
    public async Task HandleAsync(LearningActivity e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        player.AddLearningMinutes(e.Minutes, clock.GetUtcNow().UtcDateTime);
        await engine.ProgressDailyAsync(player, "minutes", e.Minutes, engine.DayOf(player, e.OccurredAt), cancellationToken);
        await engine.FinishAsync(player, cancellationToken);
    }
}

/// <summary>
/// Badge progress for first completions, and BR-118 / B-44 "Finish Brain-Vita": done for the day when a Brain-Vita
/// brief is finished for the first time that day.
/// </summary>
public sealed class BriefCompletedHandler(IGameEngine engine, TimeProvider clock) : IIntegrationEventHandler<BriefCompleted>
{
    public const string BrainVita = "brain-vita";

    public async Task HandleAsync(BriefCompleted e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        if (e.FirstCompletion)
        {
            player.Count(Counters.BriefsRead, 1, clock.GetUtcNow().UtcDateTime);
            if (string.Equals(e.CourseSlug, BrainVita, StringComparison.Ordinal))
            {
                await engine.ProgressDailyAsync(player, "brain-vita", 1, engine.DayOf(player, e.OccurredAt), cancellationToken);
            }
        }

        await engine.FinishAsync(player, cancellationToken);
    }
}

/// <summary>
/// BR-106: +1 per correct answer on the first submit only. The first submit also makes the day a streak day (§4.4,
/// B-45: learner's local date of the submit) and counts for "Score 80%+" (BR-118). Retakes and practice change nothing.
/// </summary>
public sealed class QuizSubmittedHandler(IGameEngine engine, TimeProvider clock, IOptions<GamificationOptions> options) : IIntegrationEventHandler<QuizSubmitted>
{
    public const double PassMark = 0.5;
    public const double HighMark = 0.8;

    public async Task HandleAsync(QuizSubmitted e, CancellationToken cancellationToken)
    {
        if (!e.FirstAttempt || e.Total <= 0)
        {
            return;
        }

        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var day = engine.DayOf(player, e.OccurredAt);
        await engine.MarkStreakDayAsync(player, day, cancellationToken);

        var correct = Math.Clamp(e.Correct, 0, e.Total);
        var score = (double)correct / e.Total;
        await engine.AwardAsync(player, "quiz.correct", e.QuizId, correct * options.Value.Xp.QuizCorrectAnswer, cancellationToken);
        if (score >= PassMark)
        {
            player.Count(Counters.QuizzesPassed, 1, now);
        }

        if (score >= HighMark)
        {
            player.Count(Counters.Quizzes80, 1, now);
            await engine.ProgressDailyAsync(player, "quiz-80", 1, day, cancellationToken);
        }

        if (correct == e.Total)
        {
            player.Count(Counters.QuizzesPerfect, 1, now);
        }

        await engine.FinishAsync(player, cancellationToken);
    }
}

public sealed class ModuleCompletedHandler(IGameEngine engine, TimeProvider clock) : IIntegrationEventHandler<ModuleCompleted>
{
    public async Task HandleAsync(ModuleCompleted e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        player.Count(Counters.ModulesCompleted, 1, clock.GetUtcNow().UtcDateTime);
        await engine.FinishAsync(player, cancellationToken);
    }
}

public sealed class CourseCompletedHandler(IGameEngine engine, TimeProvider clock) : IIntegrationEventHandler<CourseCompleted>
{
    public async Task HandleAsync(CourseCompleted e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        player.Count(Counters.CoursesCompleted, 1, now);
        player.CountAtLeast(Counters.CourseDone(e.CourseSlug), 1, now);
        await engine.FinishAsync(player, cancellationToken);
    }
}

/// <summary>BR-107: the Lobby first-moves bonus, +50 once per account.</summary>
public sealed class FirstMovesClaimedHandler(IGameEngine engine, IOptions<GamificationOptions> options) : IIntegrationEventHandler<FirstMovesClaimed>
{
    public Task HandleAsync(FirstMovesClaimed e, CancellationToken cancellationToken) =>
        Once.AwardAsync(engine, e.LearnerId, "first-moves", "once", options.Value.Xp.FirstMoves, cancellationToken);
}

/// <summary>BR-111: "Apply to a job" pays +100 on the first application only; withdrawing takes nothing back (BR-115).</summary>
public sealed class ApplicationSentHandler(IGameEngine engine, IOptions<GamificationOptions> options) : IIntegrationEventHandler<ApplicationSent>
{
    public Task HandleAsync(ApplicationSent e, CancellationToken cancellationToken) =>
        Once.AwardAsync(engine, e.LearnerId, "mission", "first-application", options.Value.Xp.FirstApplication, cancellationToken);
}

/// <summary>BR-112: "Book a Hire Fest slot" pays +100 on the first booking only.</summary>
public sealed class FestBookedHandler(IGameEngine engine, IOptions<GamificationOptions> options) : IIntegrationEventHandler<FestBooked>
{
    public Task HandleAsync(FestBooked e, CancellationToken cancellationToken) =>
        Once.AwardAsync(engine, e.LearnerId, "mission", "first-fest-booking", options.Value.Xp.FirstFestBooking, cancellationToken);
}

/// <summary>A one-time award: credit (the ledger key makes it once), then badges and totals.</summary>
internal static class Once
{
    public static async Task AwardAsync(IGameEngine engine, Guid learnerId, string reason, string sourceId, int xp, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(learnerId, null, null, cancellationToken);
        await engine.AwardAsync(player, reason, sourceId, xp, cancellationToken);
        await engine.FinishAsync(player, cancellationToken);
    }
}
