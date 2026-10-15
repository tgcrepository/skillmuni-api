using Learner.Application.Contracts;
using Learner.Domain;
using Microsoft.Extensions.Options;

namespace Learner.Application.Services;

public sealed record LevelInfo(int Level, string Rank, int Start, int Target);

/// <summary>
/// The progress block every XP-earning Learner response returns (quest completion, programme). One level ladder
/// everywhere (A-249), applied to the XP on display. <c>GET me/progress</c> itself is Gamification's (BR-138).
/// </summary>
public interface IProgressService
{
    LearnerProgressDto For(LearnerProfile learner);

    LevelInfo LevelFor(int xp);
}

public sealed class ProgressService(TimeProvider clock, IOptions<LearnerServiceOptions> options) : IProgressService
{
    /// <summary>How long a just-awarded amount is shown before Gamification's totals must confirm it.</summary>
    public static readonly TimeSpan EstimateWindow = TimeSpan.FromMinutes(5);

    public LearnerProgressDto For(LearnerProfile learner)
    {
        var xp = learner.DisplayXp(clock.GetUtcNow().UtcDateTime, EstimateWindow);
        var level = LevelFor(xp);

        // Every finished quest is a badge, so the count never trails the quests while Gamification catches up.
        var badges = Math.Max(learner.Badges, learner.QuestsCompleted);
        return new LearnerProgressDto(learner.QuestsCompleted, learner.PartsDone, xp, badges, level.Level, level.Rank, level.Target);
    }

    public LevelInfo LevelFor(int xp)
    {
        var ladder = options.Value.Ladder();
        var step = ladder.LastOrDefault(s => xp >= s.Min) ?? ladder[0];
        var next = ladder.FirstOrDefault(s => s.Min > xp);
        return new LevelInfo(step.Level, step.Rank, step.Min, next?.Min ?? step.Min + options.Value.LastLevelSpan);
    }
}
