using Learner.Domain;

namespace Learner.Application;

/// <summary>Learner rules. Bound from the <c>Learner</c> config section; every value has a working default.</summary>
public sealed class LearnerServiceOptions
{
    public const string Section = "Learner";

    /// <summary>
    /// The 8 registration quests. Empty means <see cref="QuestDefaults.All"/>. Config lists are appended to, not
    /// replaced, by the binder, so the defaults live in code and a config list replaces them whole.
    /// </summary>
    public List<QuestDefinition> Quests { get; set; } = [];

    public XpRules Xp { get; set; } = new();

    /// <summary>A-249 level ladder: minimum XP per level. The last level's target is its own minimum + <see cref="LastLevelSpan"/>.</summary>
    public List<LevelStep> Levels { get; set; } = [];

    public int LastLevelSpan { get; set; } = 400;

    /// <summary>Figma P4: wrong programme codes allowed before the cool-down.</summary>
    public int ProgrammeTries { get; set; } = 5;

    public TimeSpan ProgrammeCoolDown { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>A-274: feedback earns XP at most once per this period.</summary>
    public TimeSpan FeedbackXpEvery { get; set; } = TimeSpan.FromDays(30);

    /// <summary>API-034: autosave drafts are small JSON values.</summary>
    public int MaxDraftBytes { get; set; } = 64 * 1024;

    /// <summary>A quest's merged answers stay well under MySQL's JSON limits.</summary>
    public int MaxQuestAnswersBytes { get; set; } = 256 * 1024;

    public long MaxPhotoBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Same 10 MB as the CV dropzone copy.</summary>
    public long MaxTrophyFileBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Lexicon for <c>GET profile/edit</c> skillOptions, until it comes from ESCO decks.</summary>
    public List<string> SkillOptions { get; set; } = [];

    public IReadOnlyList<Quest> QuestCatalog() =>
        (Quests.Count > 0 ? Quests : QuestDefaults.All).Select(q => q.ToQuest()).ToList();

    public IReadOnlyList<LevelStep> Ladder() =>
        (Levels.Count > 0 ? Levels : LevelStep.Defaults).OrderBy(l => l.Min).ToList();

    public IReadOnlyList<string> ProfileSkillOptions() => SkillOptions.Count > 0 ? SkillOptions : QuestDefaults.SkillOptions;
}

/// <summary>
/// XP amounts the Learner screens show at once (A-249, A-274, profile strength). Gamification credits the same
/// amounts from Learner's events and is the only ledger (CLAUDE.md §9); keep both tables in step.
/// </summary>
public sealed class XpRules
{
    public int QuestStep { get; set; } = 20;

    public int QuestComplete { get; set; } = 50;

    public int Programme { get; set; } = 100;

    public int Feedback { get; set; } = 5;

    public int ProfilePhoto { get; set; } = 5;

    public int ProfileSkills { get; set; } = 10;

    public int CareerDrivers { get; set; } = 20;
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

public sealed class QuestDefinition
{
    public int Number { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Badge { get; set; } = string.Empty;

    public List<QuestPartDefinition> Parts { get; set; } = [];

    public Quest ToQuest() => new(Number, Slug, Name, Badge, Parts.Select(p => new QuestPart(p.Key, p.Label, p.Optional)).ToList());
}

public sealed class QuestPartDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public bool Optional { get; set; }
}

/// <summary>
/// Defaults taken from the frontend's quest catalogue and screen definitions (13 Oct 2026). Labels and badges are
/// flagged to confirm against <c>quest-defs.ts</c>; override in config when they change.
/// </summary>
public static class QuestDefaults
{
    public static readonly IReadOnlyList<QuestDefinition> All =
    [
        Q(1, "who-dis", "Who Dis?", "ID Unlocked", ("name", "Your name", false), ("identity", "Identity", true), ("birthday", "Birthday", false)),
        Q(2, "hit-me-up", "Hit Me Up", "Signal Boost", ("mobile", "Mobile number", false), ("backup", "Backup contact", true), ("address", "Address", false)),
        Q(3, "big-brain-stuff", "Big Brain Stuff", "Big Brain", ("level", "Education level", false), ("details", "Education details", false), ("timeline", "Timeline", true)),
        Q(4, "your-flex-era", "Your Flex Era", "Flex Mode", ("skills", "Skills", false), ("languages", "Languages", false), ("trophies", "Trophies", true)),
        Q(5, "work-lore", "Work Lore", "Lore Keeper", ("types", "Work types", false), ("chapters", "Work chapters", false)),
        Q(6, "pick-your-arc", "Pick Your Arc", "Arc Chosen", ("arc", "What you're looking for", false)),
        Q(7, "drop-the-receipts", "Drop the Receipts", "Receipts In", ("receipts", "Documents", false)),
        Q(8, "vibe-check", "Vibe Check", "Vibe Checked", ("review", "Review", false)),
    ];

    public static readonly IReadOnlyList<string> SkillOptions =
    [
        "Customer service", "Communication", "MS Excel", "Teamwork", "Problem solving", "Sales",
        "Hindi", "English", "Data entry", "Social media", "Time management", "Leadership",
    ];

    private static QuestDefinition Q(int number, string slug, string name, string badge, params (string Key, string Label, bool Optional)[] parts) => new()
    {
        Number = number,
        Slug = slug,
        Name = name,
        Badge = badge,
        Parts = parts.Select(p => new QuestPartDefinition { Key = p.Key, Label = p.Label, Optional = p.Optional }).ToList(),
    };
}
