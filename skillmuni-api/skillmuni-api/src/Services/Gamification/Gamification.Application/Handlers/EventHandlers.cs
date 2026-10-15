using Gamification.Application.Rules;
using Gamification.Application.Services;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;

namespace Gamification.Application.Handlers;

// One handler per event. Each credits XP through the engine (once per reason + source), moves counters, and calls
// FinishAsync to award badges and queue the totals event. The dispatcher saves everything with the inbox row.

public sealed class AccountRegisteredHandler(IGameEngine engine, TimeProvider clock) : IIntegrationEventHandler<AccountRegistered>
{
    public async Task HandleAsync(AccountRegistered e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.AccountId, $"{e.FirstName} {e.LastName}".Trim(), e.Market, cancellationToken);
        player.Rename($"{e.FirstName} {e.LastName}".Trim(), clock.GetUtcNow().UtcDateTime);
        await engine.FinishAsync(player, cancellationToken);
    }
}

public sealed class ClaimConfirmedHandler(IGameEngine engine, IOptions<GamificationOptions> options) : IIntegrationEventHandler<ClaimConfirmed>
{
    public async Task HandleAsync(ClaimConfirmed e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.AccountId, null, null, cancellationToken);
        await engine.AwardAsync(player, "claim.confirmed", e.ClaimId.ToString(), options.Value.Xp.ClaimConfirmed, cancellationToken);
        await engine.FinishAsync(player, cancellationToken);
    }
}

public sealed class AccountErasedHandler(IPlayerDataEraser eraser) : IIntegrationEventHandler<AccountErased>
{
    public Task HandleAsync(AccountErased e, CancellationToken cancellationToken) => eraser.EraseAsync(e.AccountId, cancellationToken);
}

public sealed class QuestStepHandler(IGameEngine engine, TimeProvider clock, IOptions<GamificationOptions> options) : IIntegrationEventHandler<QuestStepCompleted>
{
    public async Task HandleAsync(QuestStepCompleted e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        player.CountAtLeast(Counters.QuestParts(e.Slug), e.Part, clock.GetUtcNow().UtcDateTime);
        await engine.AwardAsync(player, "quest.step", $"{e.Slug}:{e.Part}", options.Value.Xp.QuestStep, cancellationToken);
        await engine.FinishAsync(player, cancellationToken);
    }
}

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

/// <summary>Programme XP, and the programme becomes the learner's Arena batch.</summary>
public sealed class ProgrammeEnrolledHandler(IGameEngine engine, TimeProvider clock, IOptions<GamificationOptions> options) : IIntegrationEventHandler<ProgrammeEnrolled>
{
    public async Task HandleAsync(ProgrammeEnrolled e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        player.JoinBatch(e.ProgrammeCode, clock.GetUtcNow().UtcDateTime);
        await engine.AwardAsync(player, "programme.enrolled", e.ProgrammeCode, options.Value.Xp.Programme, cancellationToken);
        await engine.FinishAsync(player, cancellationToken);
    }
}

/// <summary>A-274: Learner decides the once-a-month rule; the event id makes each paid feedback unique.</summary>
public sealed class FeedbackSentHandler(IGameEngine engine, IOptions<GamificationOptions> options) : IIntegrationEventHandler<FeedbackSent>
{
    public async Task HandleAsync(FeedbackSent e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        if (e.EarnsXp)
        {
            await engine.AwardAsync(player, "feedback", e.EventId.ToString(), options.Value.Xp.Feedback, cancellationToken);
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

/// <summary>Career Drivers: XP on the first attempt only (Figma S2: retakes don't earn).</summary>
public sealed class AssessmentSubmittedHandler(IGameEngine engine, TimeProvider clock, IOptions<GamificationOptions> options) : IIntegrationEventHandler<AssessmentSubmitted>
{
    public async Task HandleAsync(AssessmentSubmitted e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        player.CountAtLeast(Counters.CareerDrivers, 1, clock.GetUtcNow().UtcDateTime);
        await engine.AwardAsync(player, "assessment.career-drivers", "first", options.Value.Xp.CareerDrivers, cancellationToken);
        await engine.FinishAsync(player, cancellationToken);
    }
}

public sealed class LearningActivityHandler(IGameEngine engine, TimeProvider clock) : IIntegrationEventHandler<LearningActivity>
{
    public async Task HandleAsync(LearningActivity e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        player.AddLearningMinutes(e.Minutes, clock.GetUtcNow().UtcDateTime);
        await engine.MarkActiveAsync(player, cancellationToken);
        await engine.ProgressDailyAsync(player, "minutes", e.Minutes, cancellationToken);
        await engine.FinishAsync(player, cancellationToken);
    }
}

public sealed class BriefCompletedHandler(IGameEngine engine, TimeProvider clock) : IIntegrationEventHandler<BriefCompleted>
{
    public async Task HandleAsync(BriefCompleted e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        player.Count(Counters.BriefsRead, 1, clock.GetUtcNow().UtcDateTime);
        await engine.MarkActiveAsync(player, cancellationToken);
        await engine.ProgressDailyAsync(player, "brief", 1, cancellationToken);
        await engine.FinishAsync(player, cancellationToken);
    }
}

/// <summary>Quiz XP: +1 per correct answer on the first check only; later checks are practice (QuizResult copy).</summary>
public sealed class QuizSubmittedHandler(IGameEngine engine, TimeProvider clock, IOptions<GamificationOptions> options) : IIntegrationEventHandler<QuizSubmitted>
{
    public const double PassMark = 0.5;
    public const double HighMark = 0.8;

    public async Task HandleAsync(QuizSubmitted e, CancellationToken cancellationToken)
    {
        var player = await engine.PlayerAsync(e.LearnerId, null, null, cancellationToken);
        await engine.MarkActiveAsync(player, cancellationToken);
        if (!e.FirstAttempt || e.Total <= 0)
        {
            await engine.FinishAsync(player, cancellationToken);
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var score = (double)e.Correct / e.Total;
        await engine.AwardAsync(player, "quiz.correct", e.QuizId, e.Correct * options.Value.Xp.QuizCorrectAnswer, cancellationToken);
        if (score >= PassMark)
        {
            player.Count(Counters.QuizzesPassed, 1, now);
        }

        if (score >= HighMark)
        {
            player.Count(Counters.Quizzes80, 1, now);
            await engine.ProgressDailyAsync(player, "quiz-80", 1, cancellationToken);
        }

        if (e.Correct == e.Total)
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
