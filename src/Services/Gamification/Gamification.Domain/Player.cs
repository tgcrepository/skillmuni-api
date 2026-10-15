using System.Text.Json;

namespace Gamification.Domain;

/// <summary>
/// A learner's game state: totals kept in step with the XP ledger, the streak, badge counters and the showcase.
/// The id is the Identity account id.
/// </summary>
public sealed class Player : Entity
{
    public const int MaxShowcase = 3;

    private Dictionary<string, int>? _counters;

    private Player()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Market { get; private set; } = "IN";

    public string? PhotoUrl { get; private set; }

    /// <summary>The learner's cohort for the Arena "batch" tab: the programme they enrolled in, if any.</summary>
    public string? Batch { get; private set; }

    public int TotalXp { get; private set; }

    public int Badges { get; private set; }

    public int StreakDays { get; private set; }

    public int BestStreak { get; private set; }

    public DateOnly? LastActiveDay { get; private set; }

    public int LearningMinutes { get; private set; }

    public int CheersReceived { get; private set; }

    /// <summary>Rank on the weekly "everyone" board when the learner last opened the Arena (for "rank up").</summary>
    public int? LastSeenRank { get; private set; }

    public string ShowcaseJson { get; private set; } = "[]";

    /// <summary>Badge counters (quests completed, quizzes passed…), as JSON.</summary>
    public string CountersJson { get; private set; } = "{}";

    public DateTime JoinedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

    public IReadOnlyList<string> Showcase => JsonSerializer.Deserialize<List<string>>(ShowcaseJson) ?? [];

    public static Player Join(Guid learnerId, string tenantId, string market, string name, DateTime now) => new()
    {
        Id = learnerId,
        TenantId = tenantId,
        Market = market,
        Name = name.Trim(),
        JoinedAt = now,
        UpdatedAt = now,
    };

    public int Counter(string key) => Counters().GetValueOrDefault(key);

    public void Count(string key, int by, DateTime now)
    {
        var counters = Counters();
        counters[key] = counters.GetValueOrDefault(key) + by;
        CountersJson = JsonSerializer.Serialize(counters);
        UpdatedAt = now;
    }

    /// <summary>Counters that only move up (parts done in a quest, best score…).</summary>
    public void CountAtLeast(string key, int value, DateTime now)
    {
        var counters = Counters();
        if (value > counters.GetValueOrDefault(key))
        {
            counters[key] = value;
            CountersJson = JsonSerializer.Serialize(counters);
            UpdatedAt = now;
        }
    }

    public void Rename(string name, DateTime now)
    {
        if (name.Trim().Length > 0)
        {
            Name = name.Trim();
            UpdatedAt = now;
        }
    }

    public void SetPhoto(string? photoUrl, DateTime now)
    {
        PhotoUrl = photoUrl;
        UpdatedAt = now;
    }

    public void JoinBatch(string batch, DateTime now)
    {
        Batch = batch;
        UpdatedAt = now;
    }

    public void AddXp(int xp, DateTime now)
    {
        TotalXp += xp;
        UpdatedAt = now;
    }

    public void BadgeEarned(DateTime now)
    {
        Badges++;
        UpdatedAt = now;
    }

    /// <summary>
    /// Marks <paramref name="today"/> as a streak day. Consecutive days grow the streak; a gap restarts it at 1.
    /// A day before the last streak day (a late event) changes nothing.
    /// </summary>
    /// <returns>True the first time <paramref name="today"/> counts.</returns>
    public bool MarkActive(DateOnly today, DateTime now)
    {
        if (LastActiveDay is { } last && today <= last)
        {
            return false;
        }

        StreakDays = LastActiveDay == today.AddDays(-1) ? StreakDays + 1 : 1;
        BestStreak = Math.Max(BestStreak, StreakDays);
        LastActiveDay = today;
        UpdatedAt = now;
        return true;
    }

    /// <summary>The streak shown today: still alive if the learner was active yesterday or today.</summary>
    public int CurrentStreak(DateOnly today) =>
        LastActiveDay is { } last && last >= today.AddDays(-1) ? StreakDays : 0;

    public void AddLearningMinutes(int minutes, DateTime now)
    {
        LearningMinutes += Math.Max(0, minutes);
        UpdatedAt = now;
    }

    public void Cheered(bool on, DateTime now)
    {
        CheersReceived = Math.Max(0, CheersReceived + (on ? 1 : -1));
        UpdatedAt = now;
    }

    public void SawRank(int rank) => LastSeenRank = rank;

    public void SetShowcase(IReadOnlyList<string> badgeIds, DateTime now)
    {
        ShowcaseJson = JsonSerializer.Serialize(badgeIds.Distinct(StringComparer.Ordinal).Take(MaxShowcase).ToList());
        UpdatedAt = now;
    }

    private Dictionary<string, int> Counters() =>
        _counters ??= JsonSerializer.Deserialize<Dictionary<string, int>>(CountersJson) ?? new Dictionary<string, int>();
}
