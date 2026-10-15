namespace Gamification.Application;

/// <summary>Game rules. Bound from the <c>Gamification</c> config section; every value has a working default.</summary>
public sealed class GamificationOptions
{
    public const string Section = "Gamification";

    /// <summary>A-249: one level ladder for the platform. Empty = <see cref="LevelStep.Defaults"/>.</summary>
    public List<LevelStep> Levels { get; set; } = [];

    public int LastLevelSpan { get; set; } = 400;

    public XpTable Xp { get; set; } = new();

    /// <summary>The 8 registration quests, for their badges. Empty = <see cref="QuestBadge.Defaults"/>.</summary>
    public List<QuestBadge> Quests { get; set; } = [];

    /// <summary>Three daily quests, reset at local midnight (MISS-03). Empty = <see cref="DailyQuestDefinition.Defaults"/>.</summary>
    public List<DailyQuestDefinition> DailyQuests { get; set; } = [];

    public ArenaOptions Arena { get; set; } = new();

    /// <summary>"Founding Learner": joined before this date.</summary>
    public DateOnly FoundingUntil { get; set; } = new(2027, 10, 1);

    /// <summary>Market → IANA time zone for day boundaries (streaks, daily quests, weekly boards).</summary>
    public Dictionary<string, string> TimeZones { get; set; } = new(StringComparer.Ordinal)
    {
        ["IN"] = "Asia/Kolkata",
        ["ZA"] = "Africa/Johannesburg",
    };

    public IReadOnlyList<LevelStep> Ladder() => (Levels.Count > 0 ? Levels : LevelStep.Defaults).OrderBy(l => l.Min).ToList();

    public IReadOnlyList<QuestBadge> QuestBadges() => (Quests.Count > 0 ? Quests : QuestBadge.Defaults).OrderBy(q => q.Number).ToList();

    public IReadOnlyList<DailyQuestDefinition> Dailies() => DailyQuests.Count > 0 ? DailyQuests : DailyQuestDefinition.Defaults;

    public TimeZoneInfo TimeZoneFor(string market) =>
        TimeZoneInfo.FindSystemTimeZoneById(TimeZones.GetValueOrDefault(market, TimeZones.GetValueOrDefault("IN", "Asia/Kolkata")));
}

/// <summary>
/// XP per reason. The amounts the Learner screens show at once (A-249, A-274) must match; this table is the source
/// of truth for what is credited.
/// </summary>
public sealed class XpTable
{
    public int QuestStep { get; set; } = 20;

    public int QuestComplete { get; set; } = 50;

    public int ProfilePhoto { get; set; } = 5;

    public int ProfileSkills { get; set; } = 10;

    public int Programme { get; set; } = 100;

    public int Feedback { get; set; } = 5;

    public int CareerDrivers { get; set; } = 20;

    /// <summary>Confirming an institute profile (claim V4). Flagged: amount to confirm.</summary>
    public int ClaimConfirmed { get; set; } = 100;

    /// <summary>Quizzes: +1 per correct answer on the first check, no bonus (QuizResult copy).</summary>
    public int QuizCorrectAnswer { get; set; } = 1;
}

public sealed class LevelStep
{
    public static readonly IReadOnlyList<LevelStep> Defaults =
    [
        new() { Level = 1, Min = 0, Rank = "Rookie" },
        new() { Level = 2, Min = 400, Rank = "Pathfinder" },
        new() { Level = 3, Min = 800, Rank = "Trailblazer" },
        new() { Level = 4, Min = 1200, Rank = "Legend" },
        new() { Level = 5, Min = 1600, Rank = "Icon" },
    ];

    public int Level { get; set; }

    public int Min { get; set; }

    public string Rank { get; set; } = string.Empty;
}

public sealed class QuestBadge
{
    public static readonly IReadOnlyList<QuestBadge> Defaults =
    [
        new() { Number = 1, Slug = "who-dis", Name = "Who Dis?", Badge = "ID Unlocked", Parts = 3 },
        new() { Number = 2, Slug = "hit-me-up", Name = "Hit Me Up", Badge = "Signal Boost", Parts = 3 },
        new() { Number = 3, Slug = "big-brain-stuff", Name = "Big Brain Stuff", Badge = "Big Brain", Parts = 3 },
        new() { Number = 4, Slug = "your-flex-era", Name = "Your Flex Era", Badge = "Flex Mode", Parts = 3 },
        new() { Number = 5, Slug = "work-lore", Name = "Work Lore", Badge = "Lore Keeper", Parts = 2 },
        new() { Number = 6, Slug = "pick-your-arc", Name = "Pick Your Arc", Badge = "Arc Chosen", Parts = 1 },
        new() { Number = 7, Slug = "drop-the-receipts", Name = "Drop the Receipts", Badge = "Receipts In", Parts = 1 },
        new() { Number = 8, Slug = "vibe-check", Name = "Vibe Check", Badge = "Vibe Checked", Parts = 1 },
    ];

    public int Number { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Badge { get; set; } = string.Empty;

    public int Parts { get; set; }
}

/// <summary>A daily quest: <c>Kind</c> is <c>minutes</c> (learning time), <c>quiz-80</c> or <c>brief</c>.</summary>
public sealed class DailyQuestDefinition
{
    public static readonly IReadOnlyList<DailyQuestDefinition> Defaults =
    [
        new() { Id = "learn-15", Kind = "minutes", Icon = "clock", Title = "Learn for 15 minutes", Target = 15, Xp = 5, Link = "/home", Unit = "min" },
        new() { Id = "quiz-80", Kind = "quiz-80", Icon = "lightbulb", Title = "Score 80%+ on any quiz", Target = 1, Xp = 5, Link = "/home" },
        new() { Id = "brief-1", Kind = "brief", Icon = "play", Title = "Finish a brief", Target = 1, Xp = 10, Link = "/home" },
    ];

    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Icon { get; set; } = "star";

    public string Title { get; set; } = string.Empty;

    public int Target { get; set; } = 1;

    public int Xp { get; set; }

    public string Link { get; set; } = "/home";

    /// <summary>Shown after the count, e.g. "6 / 15 min".</summary>
    public string? Unit { get; set; }
}

/// <summary>Arena values the design marks "to confirm" (handoff 58:1530, MISS-11).</summary>
public sealed class ArenaOptions
{
    public int BoardSize { get; set; } = 50;

    public int StreakBonus { get; set; } = 5;

    public int PromotionRank { get; set; } = 3;

    public string League { get; set; } = "Gold League";

    public TimeSpan ChallengeLength { get; set; } = TimeSpan.FromDays(7);

    /// <summary>The shared "everyone" boards are cached this long (Redis).</summary>
    public TimeSpan CacheFor { get; set; } = TimeSpan.FromSeconds(60);
}
