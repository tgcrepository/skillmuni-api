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

    /// <summary>Three daily quests, reset at local midnight (BR-118). Empty = <see cref="DailyQuestDefinition.Defaults"/>.</summary>
    public List<DailyQuestDefinition> DailyQuests { get; set; } = [];

    public ArenaOptions Arena { get; set; } = new();

    public BadgeOptions Badges { get; set; } = new();

    /// <summary>BR-114: feedback pays at most once per this period (Gamification decides, from its ledger).</summary>
    public TimeSpan FeedbackXpEvery { get; set; } = TimeSpan.FromDays(30);

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
/// XP per reason: BRD §4.1, the final rulebook (an action not listed earns nothing). The amounts the Learner screens
/// show at once must match; this table is the source of truth for what is credited. Daily quests carry their own XP.
/// </summary>
public sealed class XpTable
{
    /// <summary>BR-100 / BR-101.</summary>
    public int QuestStep { get; set; } = 20;

    /// <summary>BR-102.</summary>
    public int QuestComplete { get; set; } = 50;

    /// <summary>BR-104: assessment islands 1–3, first attempt.</summary>
    public int IslandCleared { get; set; } = 25;

    /// <summary>BR-105: Career Drivers submitted (pays island 4), first attempt.</summary>
    public int AssessmentSubmitted { get; set; } = 25;

    /// <summary>BR-106: +1 per correct answer on a brief quiz's first submit.</summary>
    public int QuizCorrectAnswer { get; set; } = 1;

    /// <summary>BR-107.</summary>
    public int FirstMoves { get; set; } = 50;

    /// <summary>BR-108.</summary>
    public int ProfilePhoto { get; set; } = 5;

    /// <summary>BR-109.</summary>
    public int ProfileSkills { get; set; } = 10;

    /// <summary>BR-110: the Job Profile first reaches 100%.</summary>
    public int JobProfileComplete { get; set; } = 50;

    /// <summary>BR-111.</summary>
    public int FirstApplication { get; set; } = 100;

    /// <summary>BR-112.</summary>
    public int FirstFestBooking { get; set; } = 100;

    /// <summary>BR-113: once per learner.</summary>
    public int Programme { get; set; } = 100;

    /// <summary>BR-114: at most once per <see cref="GamificationOptions.FeedbackXpEvery"/>.</summary>
    public int Feedback { get; set; } = 5;
}

/// <summary>§4.3: Phase 1 awards the 8 quest badges only; the 48 catalogue badges show progress until the rules engine ships.</summary>
public sealed class BadgeOptions
{
    /// <summary>Award catalogue badges when their goal is met (MISS-02). Off for launch.</summary>
    public bool AwardCatalogue { get; set; }
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

/// <summary>A registration quest and its badge (BRD §3.3, §4.3).</summary>
public sealed class QuestBadge
{
    public static readonly IReadOnlyList<QuestBadge> Defaults =
    [
        new() { Number = 1, Slug = "who-dis", Name = "Who Dis?", Badge = "Main Character Badge", Parts = 3 },
        new() { Number = 2, Slug = "hit-me-up", Name = "Hit Me Up", Badge = "Link Up Badge", Parts = 3 },
        new() { Number = 3, Slug = "big-brain-stuff", Name = "Big Brain Stuff", Badge = "Big Brain Badge", Parts = 3 },
        new() { Number = 4, Slug = "your-flex-era", Name = "Your Flex Era", Badge = "Player Card", Parts = 3 },
        new() { Number = 5, Slug = "work-lore", Name = "Work Lore", Badge = "Storyteller Badge", Parts = 3 },
        new() { Number = 6, Slug = "pick-your-arc", Name = "Pick Your Arc", Badge = "Arc Badge", Parts = 1 },
        new() { Number = 7, Slug = "drop-the-receipts", Name = "Drop the Receipts", Badge = "Receipts Badge", Parts = 1 },
        new() { Number = 8, Slug = "vibe-check", Name = "Vibe Check", Badge = "Vibe Check Badge", Parts = 1 },
    ];

    public int Number { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Badge { get; set; } = string.Empty;

    public int Parts { get; set; }
}

/// <summary>
/// A daily quest (BR-118): <c>Kind</c> is <c>brain-vita</c> (a Brain-Vita brief finished for the first time, B-44),
/// <c>minutes</c> (learning time) or <c>quiz-80</c> (80%+ on a quiz's first submit).
/// </summary>
public sealed class DailyQuestDefinition
{
    public static readonly IReadOnlyList<DailyQuestDefinition> Defaults =
    [
        // B-44: resume Brain-Vita where the learner left off. Gamification can't know the resume point, so the link is
        // the course; the frontend / Learning resolves the next unfinished brief.
        new() { Id = "finish-bv", Kind = "brain-vita", Icon = "play", Title = "Finish Brain-Vita", Target = 1, Xp = 10, Link = "/learn/brain-vita", Label = "Resume where you left off" },
        new() { Id = "learn-15", Kind = "minutes", Icon = "clock", Title = "Learn for 15 minutes", Target = 15, Xp = 5, Link = "/home", Unit = "min" },
        new() { Id = "quiz-80", Kind = "quiz-80", Icon = "lightbulb", Title = "Score 80%+ on any quiz", Target = 1, Xp = 5, Link = "/learn/brain-vita/non-verbal-reasoning/quiz" },
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

    /// <summary>A fixed progress label shown until the quest is done, instead of the count.</summary>
    public string? Label { get; set; }
}

/// <summary>Arena values the design marks "to confirm" (handoff 58:1530, MISS-11).</summary>
public sealed class ArenaOptions
{
    public int BoardSize { get; set; } = 50;

    public int PromotionRank { get; set; } = 3;

    public string League { get; set; } = "Gold League";

    public TimeSpan ChallengeLength { get; set; } = TimeSpan.FromDays(7);

    /// <summary>The shared "everyone" boards are cached this long (Redis).</summary>
    public TimeSpan CacheFor { get; set; } = TimeSpan.FromSeconds(60);
}
