namespace Gamification.Application.Rules;

/// <summary>Counter keys kept on the player. Badges are earned when a counter reaches its goal.</summary>
public static class Counters
{
    public const string QuestsCompleted = "quests.completed";
    public const string QuizzesPassed = "quizzes.passed";
    public const string QuizzesPerfect = "quizzes.perfect";
    public const string Quizzes80 = "quizzes.80";
    public const string BriefsRead = "briefs.read";
    public const string BriefsBookmarked = "briefs.bookmarked";
    public const string ModulesCompleted = "modules.completed";
    public const string CoursesCompleted = "courses.completed";
    public const string CheersGiven = "cheers.given";
    public const string CareerDrivers = "assessment.career-drivers";

    /// <summary>Derived from the player, not stored: total XP and best streak.</summary>
    public const string TotalXp = "xp.total";
    public const string BestStreak = "streak.best";
    public const string Founding = "founding";

    /// <summary>Parts done in one quest, for its badge's progress.</summary>
    public static string QuestParts(string slug) => $"quest.{slug}.parts";

    public static string QuestDone(string slug) => $"quest.{slug}.done";

    public static string CourseDone(string slug) => $"course.{slug}.done";
}

/// <summary>
/// A badge and its rule. <see cref="Counter"/> null = not trackable yet (its source service isn't built); it shows
/// as locked. <see cref="Tiers"/> = tiered badge (Streak Master 3 · 7 · 30).
/// </summary>
public sealed record BadgeDefinition(
    string Id,
    string Name,
    string Icon,
    string Category,
    string Rarity,
    string HowTo,
    string? Counter = null,
    int Goal = 1,
    string? Unit = null,
    string? CtaLabel = null,
    string? CtaUrl = null,
    bool Secret = false,
    IReadOnlyList<int>? Tiers = null,
    string? Image = null)
{
    public bool Tracked => Counter is not null;

    /// <summary>One of the 8 registration-quest badges, the only ones Phase 1 awards (§4.3).</summary>
    public bool IsQuest => Id.StartsWith(BadgeCatalogue.QuestPrefix, StringComparison.Ordinal);
}

/// <summary>
/// The badge library (BRD §4.3, Figma My Badges 170:4660): one badge per registration quest (A-292) plus the 48-badge
/// catalogue, with rules where their events exist. Only the quest badges are awarded in Phase 1.
/// </summary>
public static class BadgeCatalogue
{
    private const string Quiz = "/learn/brain-vita/non-verbal-reasoning/quiz";
    private const string Home = "/home";

    private static readonly string[] QuestIcons = ["user", "phone", "lightbulb", "star", "briefcase", "compass", "paperclip", "shield-check"];

    private static readonly string[] QuestRarity = ["common", "common", "common", "rare", "rare", "rare", "epic", "legendary"];

    public static readonly IReadOnlyList<BadgeDefinition> Fixed =
    [
        // Learning
        new("first-steps", "First Steps", "star", "learning", "legendary", "Finish your first brief.", Counters.BriefsRead, 1),
        new("brain-vita-boss", "Brain-Vita Boss", "trophy", "learning", "epic", "Finish every Brain-Vita module: verbal, non-verbal, arithmetic and data.", Counters.CourseDone("brain-vita"), 1, CtaLabel: "Finish Brain-Vita", CtaUrl: "/learn/brain-vita"),
        new("knook-explorer", "Knook Explorer", "target", "learning", "rare", "Open a module in 5 different Knooks.", Goal: 5, Unit: "Knooks", CtaLabel: "Explore Knooks", CtaUrl: Home),
        new("cpd-champ", "CPD Champ", "shield-check", "learning", "rare", "Earn 10 CPD hours from finished courses.", Goal: 10, Unit: "hrs"),
        new("hospitality-hero", "Hospitality Hero", "briefcase", "learning", "rare", "Finish the Hospitality course.", Counters.CourseDone("hospitality")),
        new("it-gyan-guru", "IT Gyan Guru", "lightbulb", "learning", "rare", "Finish the IT Gyan course.", Counters.CourseDone("it-gyan")),
        new("banking-pro", "Banking Pro", "coins", "learning", "epic", "Finish the Banking course.", Counters.CourseDone("banking")),
        new("globe-trotter", "Globe Trotter", "compass", "learning", "epic", "Finish International Opportunities.", Counters.CourseDone("intl-opps")),
        new("auto-ace", "Auto Ace", "zap", "learning", "epic", "Finish Auto Components.", Counters.CourseDone("auto-components")),
        new("module-master", "Module Master", "layout-grid", "learning", "rare", "Finish 10 modules.", Counters.ModulesCompleted, 10),
        new("course-closer", "Course Closer", "check-circle", "learning", "common", "Finish your first full course.", Counters.CoursesCompleted, 1),
        new("bookworm", "Bookworm", "bookmark", "learning", "common", "Bookmark 10 briefs.", Counters.BriefsBookmarked, 10),
        new("deep-diver", "Deep Diver", "play", "learning", "rare", "Read 50 briefs.", Counters.BriefsRead, 50),
        new("secret-learning", "Secret", "star", "learning", "epic", "Keep learning. This one is a surprise.", Secret: true),

        // Quizzes
        new("quiz-starter", "Quiz Starter", "check-circle", "quizzes", "epic", "Pass your first quiz.", Counters.QuizzesPassed, 1, CtaLabel: "Take a quiz", CtaUrl: Quiz),
        new("ten-quizzes", "Quiz Regular", "lightbulb", "quizzes", "common", "Pass 10 quizzes.", Counters.QuizzesPassed, 10, CtaLabel: "Take a quiz", CtaUrl: Quiz),
        new("perfect-score", "Perfect Score", "star", "quizzes", "rare", "Get every answer right on a quiz.", Counters.QuizzesPerfect, 1),
        new("sharp-shooter", "Sharp Shooter", "target", "quizzes", "rare", "Score 80%+ on 3 quizzes.", Counters.Quizzes80, 3),
        new("quiz-streak", "On a Roll", "flame", "quizzes", "epic", "Pass a quiz 5 days in a row.", Goal: 5, Unit: "days"),
        new("quick-thinker", "Quick Thinker", "clock", "quizzes", "rare", "Finish a quiz in under a minute."),
        new("retry-hero", "Retry Hero", "repeat", "quizzes", "common", "Get a missed question right in practice."),
        new("fifty-quizzes", "Quiz Legend", "trophy", "quizzes", "legendary", "Pass 50 quizzes.", Counters.QuizzesPassed, 50),
        new("night-quiz", "Midnight Quizzer", "sparkle", "quizzes", "rare", "Pass a quiz after 10 pm."),
        new("secret-quiz", "Secret", "star", "quizzes", "epic", "Keep learning. This one is a surprise.", Secret: true),

        // Streaks
        new("streak-starter", "Streak Starter", "flame", "streaks", "rare", "Learn 3 days in a row.", Counters.BestStreak, 3, Unit: "days"),
        new("streak-master", "Streak Master", "flame", "streaks", "legendary", "Keep a 3, 7 and 30-day streak to fill all three pips.", Counters.BestStreak, 30, Unit: "days", Tiers: [3, 7, 30]),
        new("night-owl", "Night Owl", "flame", "streaks", "rare", "Learn after 10 pm on 5 nights.", Goal: 5, Unit: "nights"),
        new("comeback-kid", "Comeback Kid", "flame", "streaks", "common", "Return after 7 days away."),
        new("early-bird", "Early Bird", "clock", "streaks", "rare", "Learn before 8 am on 5 mornings.", Goal: 5, Unit: "mornings"),
        new("secret-streak", "Secret", "star", "streaks", "epic", "Keep learning. This one is a surprise.", Secret: true),

        // Assessments
        new("card-collector", "Card Collector", "layout-grid", "assessments", "epic", "Unlock 25 skill cards.", Goal: 25),
        new("expert-eye", "Expert Eye", "crown", "assessments", "legendary", "Reach Expert on 5 skill cards.", Goal: 5),
        new("tier-up", "Tier Up", "trend-up", "assessments", "rare", "Move a skill card up a tier."),
        new("all-rounder", "All-Rounder", "target", "assessments", "epic", "Reach Advanced in 4 different skills.", Goal: 4),
        new("career-clarity", "Career Clarity", "compass", "assessments", "common", "Finish Career Drivers.", Counters.CareerDrivers, 1, CtaLabel: "Take Career Drivers", CtaUrl: "/assessments/career"),
        new("digital-native", "Digital Native", "zap", "assessments", "rare", "Score 90%+ in Digital Skills."),
        new("leader-board", "Born Leader", "users", "assessments", "rare", "Score 90%+ in Leadership."),
        new("secret-assess", "Secret", "star", "assessments", "epic", "Keep learning. This one is a surprise.", Secret: true),

        // Social
        new("cheerleader", "Cheerleader", "thumbs-up", "social", "common", "Cheer 10 players in the Arena.", Counters.CheersGiven, 10, CtaLabel: "Open the Arena", CtaUrl: "/arena"),
        new("challenger", "Challenger", "trophy", "social", "rare", "Win 3 Arena challenges.", Goal: 3),
        new("top-ten", "Top 10", "crown", "social", "epic", "Finish a week in your batch’s top 10."),
        new("squad-up", "Squad Up", "users", "social", "common", "Add 3 friends.", Goal: 3),
        new("helping-hand", "Helping Hand", "message-circle", "social", "rare", "Share a brief with a friend 5 times.", Goal: 5),
        new("secret-social", "Secret", "star", "social", "epic", "Keep learning. This one is a surprise.", Secret: true),

        // Special
        new("xp-1k", "XP 1K Club", "zap", "special", "rare", "Earn 1,000 XP in total.", Counters.TotalXp, 1000, Unit: "XP"),
        new("founding-learner", "Founding Learner", "sparkle", "special", "legendary", "Join in Skillmuni’s first year.", Counters.Founding, 1),
        new("festive-2026", "Festive Feat", "gift", "special", "epic", "Learn on 5 days during the festive week.", Goal: 5, Unit: "days"),
        new("secret-special", "Secret", "star", "special", "epic", "Keep learning. This one is a surprise.", Secret: true),
    ];

    /// <summary>Quest badges first (as the library shows them), then the fixed catalogue.</summary>
    public static IReadOnlyList<BadgeDefinition> All(IReadOnlyList<QuestBadge> quests) =>
        quests.Select((q, i) => new BadgeDefinition(
                QuestBadgeId(q.Slug),
                q.Badge,
                QuestIcons[Math.Min(i, QuestIcons.Length - 1)],
                "quests",
                QuestRarity[Math.Min(i, QuestRarity.Length - 1)],
                $"Finish the {q.Name} quest.",
                Counters.QuestDone(q.Slug),
                1,
                CtaLabel: "Continue quest",
                CtaUrl: $"/quests/{q.Slug}",
                Image: $"assets/quests/quest-{q.Number}.png"))
            .Concat(Fixed)
            .ToList();

    public const string QuestPrefix = "quest-";

    public static string QuestBadgeId(string slug) => $"{QuestPrefix}{slug}";
}
