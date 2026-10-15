using Microsoft.Extensions.Options;

namespace Muni.Application.Services;

/// <summary>Builds <see cref="LearnerSnapshot"/>: Muni's event copies plus Learner's profile completeness.</summary>
public interface ILearnerSnapshotBuilder
{
    Task<LearnerSnapshot> BuildAsync(LearnerRef who, CancellationToken cancellationToken);
}

public sealed class LearnerSnapshotBuilder(
    IMuniLearnerRepository learners,
    IProfileProgressClient profileProgress,
    IOptions<MuniOptions> options) : ILearnerSnapshotBuilder
{
    public async Task<LearnerSnapshot> BuildAsync(LearnerRef who, CancellationToken cancellationToken)
    {
        var known = await learners.FindAsync(who.Id, cancellationToken);
        var progress = await profileProgress.GetAsync(who.Id, cancellationToken);
        var ladder = options.Value.Ladder();

        var first = FirstOf(known?.FirstName, who.FirstName);
        var last = known?.LastName ?? string.Empty;
        var full = string.IsNullOrEmpty(last) ? first : $"{first} {last}";

        var xp = known?.TotalXp ?? 0;
        var level = known is { Level: > 0 } ? known.Level : ladder[0].Level;
        var rank = known is { Rank.Length: > 0 } ? known.Rank : ladder[0].Rank;
        var nextLevelXp = known is { NextLevelXp: > 0 } ? known.NextLevelXp : (ladder.Count > 1 ? ladder[1].Min : 0);

        var gaps = (progress?.Quests ?? [])
            .Where(q => q.Left.Count > 0)
            .OrderBy(q => q.Id)
            .Select(q => new MuniGap(q.Name, string.Join(", ", q.Left.Select(s => s.Label)), $"/quests/{q.Slug}?part={q.Left[0].Part}"))
            .ToList();

        return new LearnerSnapshot(
            who.Id,
            first,
            full,
            Initials(first, last),
            Fresh: known?.Archetype is null,
            xp,
            level,
            rank,
            ToNext: Math.Max(0, nextLevelXp - xp),
            QuestsDone: known?.QuestsDone ?? 0,
            ProfilePercent: progress?.Percent ?? 0,
            gaps,
            known?.Archetype,
            Streak: known?.StreakDays ?? 0,
            known?.JoinedAt);
    }

    internal static string FirstOf(string? fromEvents, string fromToken)
    {
        var first = !string.IsNullOrWhiteSpace(fromEvents) ? fromEvents : fromToken;
        return string.IsNullOrWhiteSpace(first) ? "there" : first.Trim();
    }

    internal static string Initials(string first, string last)
    {
        var a = first.Length > 0 ? first[..1].ToUpperInvariant() : string.Empty;
        var b = last.Length > 0 ? last[..1].ToUpperInvariant() : string.Empty;
        return a + b;
    }
}
