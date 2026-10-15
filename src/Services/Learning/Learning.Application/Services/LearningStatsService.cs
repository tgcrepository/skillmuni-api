using System.Globalization;
using Learning.Application.Contracts;
using Microsoft.Extensions.Options;

namespace Learning.Application.Services;

/// <summary>API-130: My Stats → Learning (Quest Log). One course card per catalogue module.</summary>
public interface ILearningStatsService
{
    Task<LearningDashboardDto> LearningAsync(Guid learnerId, CancellationToken cancellationToken);
}

public sealed class LearningStatsService(
    ILearningContent content,
    IBriefProgressRepository progress,
    IOptions<LearningOptions> options) : ILearningStatsService
{
    public const string InProgress = "in-progress";
    public const string NotStarted = "not-started";
    public const string Done = "done";

    public async Task<LearningDashboardDto> LearningAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var passed = await progress.ListPassedAsync(learnerId, cancellationToken);
        var courses = content.Modules.Select(module =>
        {
            var state = new ModuleProgress(module, passed);
            var status = state.QuizzesDone == 0 ? NotStarted : state.Completed ? Done : InProgress;
            var xpLeft = module.Topics.Sum(t => Enumerable.Range(0, t.Briefs.Count).Where(i => !state.IsPassed(t, i)).Sum(i => t.QuestionsFor(i).Count));
            return new LearningCourseDto(
                module.Slug,
                module.Title,
                module.Tone,
                Percent(state.QuizzesDone, state.QuizzesTotal),
                status,
                "Standard",
                xpLeft,
                DeadlineDays: null,
                TimeEstHours: null,
                CpdHours: null,
                module.Tagline,
                Links.Module(module.Slug));
        }).ToList();

        // Closest to done first: in progress (highest percentage first), then not started, then done.
        var sorted = courses
            .OrderBy(c => c.Status switch { InProgress => 0, NotStarted => 1, _ => 2 })
            .ThenByDescending(c => c.Status == InProgress ? c.Percent : 0)
            .ToList();

        var last = passed.OrderByDescending(p => p.LastSubmittedAt).FirstOrDefault()?.ModuleSlug;
        var current = sorted.FirstOrDefault(c => c.Id == last && c.Status == InProgress)?.Id;
        var nudge = sorted.FirstOrDefault(c => c.Status == InProgress && c.Percent >= options.Value.NudgeAtPercent) is { } close
            ? $"{close.Title} is {close.Percent.ToString("0.##", CultureInfo.InvariantCulture)}% done. One more push for the badge!"
            : null;
        return new LearningDashboardDto(sorted, current, nudge);
    }

    /// <summary>Two decimals, rounded down, so a module never shows 100% before its last brief (e.g. 98.86).</summary>
    internal static double Percent(int done, int total) =>
        total == 0 ? 0 : Math.Floor(done * 10000.0 / total) / 100;
}
