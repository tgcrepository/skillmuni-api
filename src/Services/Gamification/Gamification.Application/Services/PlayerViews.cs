using System.Globalization;
using Gamification.Application.Rules;
using Gamification.Domain;

namespace Gamification.Application.Services;

/// <summary>Pieces several screens share: the Mon→Sun streak week, today's quests, badge states.</summary>
public static class PlayerViews
{
    public static DateOnly Monday(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    /// <summary>Mon → Sun: done (active), today, missed (past, inactive), upcoming.</summary>
    public static IReadOnlyList<string> Week(DateOnly today, IReadOnlyCollection<DateOnly> activeDays)
    {
        var monday = Monday(today);
        return Enumerable.Range(0, 7).Select(i =>
        {
            var day = monday.AddDays(i);
            return day > today ? "upcoming"
                : activeDays.Contains(day) ? "done"
                : day == today ? "today"
                : "missed";
        }).ToList();
    }

    public static DailyQuestsDto Daily(IReadOnlyList<DailyQuestDefinition> quests, IReadOnlyList<DailyProgress> progress, DateTime resetsAt) =>
        new(resetsAt, quests.Select(q =>
        {
            var p = progress.FirstOrDefault(x => x.QuestId == q.Id);
            var value = Math.Min(p?.Value ?? 0, q.Target);
            var done = p?.Completed == true;
            var label = q.Unit is null ? $"{value} / {q.Target}" : $"{value} / {q.Target} {q.Unit}";
            return new DailyQuestDto(q.Id, q.Icon, q.Title, label, Math.Round(value * 100.0 / q.Target, 2), q.Xp, done, q.Link);
        }).ToList());

    /// <summary>The next local midnight, in UTC.</summary>
    public static DateTime NextMidnight(DateOnly today, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);

    public static DateTime StartOf(DateOnly day, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), zone);

    public static BadgeDto Badge(
        BadgeDefinition badge,
        Player player,
        GamificationOptions rules,
        IReadOnlyDictionary<string, DateTime> earned,
        IReadOnlyDictionary<string, int> owners,
        int playerCount)
    {
        var earnedAt = earned.TryGetValue(badge.Id, out var at) ? at : (DateTime?)null;
        var value = BadgeProgress.Value(player, badge, rules);

        // A quest badge shows progress from the parts done in that quest.
        var questSlug = badge.Id.StartsWith("quest-", StringComparison.Ordinal) ? badge.Id["quest-".Length..] : null;
        var questParts = questSlug is null ? 0 : player.Counter(Counters.QuestParts(questSlug));
        var questTotal = questSlug is null ? 0 : rules.QuestBadges().FirstOrDefault(q => q.Slug == questSlug)?.Parts ?? 0;

        string state;
        if (earnedAt is not null)
        {
            state = "earned";
        }
        else if (badge.Secret)
        {
            state = "secret";
        }
        else if (value > 0 || questParts > 0)
        {
            state = "progress";
        }
        else
        {
            state = "locked";
        }

        double percent;
        string? label;
        if (earnedAt is not null)
        {
            (percent, label) = (100, null);
        }
        else if (questSlug is not null)
        {
            percent = questTotal == 0 ? 0 : Math.Round(Math.Min(questParts, questTotal) * 100.0 / questTotal, 2);
            label = questParts > 0 ? $"{Math.Min(questParts, questTotal)} / {questTotal} parts" : null;
        }
        else if (badge.Secret || badge.Goal <= 1 && !badge.Tracked)
        {
            (percent, label) = (0, null);
        }
        else
        {
            var shown = Math.Min(value, badge.Goal);
            percent = Math.Round(shown * 100.0 / badge.Goal, 2);
            label = badge.Goal == 1 ? $"{shown} / 1" : badge.Unit is null ? $"{shown} / {badge.Goal}" : $"{shown} / {badge.Goal} {badge.Unit}";
        }

        var tiers = badge.Tiers is { } t ? new BadgeTiersDto(t.Count(goal => player.BestStreak >= goal), t.Count) : null;
        var cta = earnedAt is null && badge.CtaLabel is not null && badge.CtaUrl is not null ? new BadgeCtaDto(badge.CtaLabel, badge.CtaUrl) : null;
        var ownedPct = playerCount == 0 ? 0 : Math.Round(owners.GetValueOrDefault(badge.Id) * 100.0 / playerCount, 1);
        return new BadgeDto(
            badge.Id,
            badge.Name,
            badge.Icon,
            badge.Category,
            badge.Rarity,
            state,
            earnedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            label,
            percent,
            badge.HowTo,
            ownedPct,
            cta,
            tiers,
            player.Showcase.Contains(badge.Id, StringComparer.Ordinal));
    }

    /// <summary>MISS-01 ledger labels.</summary>
    public static string ReasonLabel(string reason) => reason switch
    {
        "quest.step" => "Quest step",
        "quest.complete" => "Quest finished",
        "profile.task" => "Profile",
        "programme.enrolled" => "Programme joined",
        "feedback" => "Feedback",
        "claim.confirmed" => "Institute profile confirmed",
        "assessment.career-drivers" => "Career Drivers",
        "quiz.correct" => "Quiz",
        _ when reason.StartsWith("daily.", StringComparison.Ordinal) => "Daily quest",
        _ => reason,
    };

    public static string FirstName(string name) => name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name;
}
