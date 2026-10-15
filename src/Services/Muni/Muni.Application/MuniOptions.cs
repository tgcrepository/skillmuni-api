namespace Muni.Application;

/// <summary>Bound from the <c>Muni</c> config section; every value has a working default.</summary>
public sealed class MuniOptions
{
    public const string Section = "Muni";

    /// <summary>Chat history and ratings older than this are deleted (BRD §9: retention still open).</summary>
    public int HistoryDays { get; set; } = 90;

    /// <summary>A question more than this after the previous line starts a new conversation in the history.</summary>
    public TimeSpan ConversationGap { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Conversations shown on the history screen.</summary>
    public int HistoryConversations { get; set; } = 20;

    /// <summary>Most recent lines read to build the history screen.</summary>
    public int HistoryLines { get; set; } = 400;

    public CleanupOptions Cleanup { get; set; } = new();

    /// <summary>Display copy of the A-249 level ladder (Gamification is the source of truth for levels).</summary>
    public List<LadderStep> Levels { get; set; } = [];

    /// <summary>XP amounts quoted in answers; must match Gamification's table (BRD §4.1).</summary>
    public int QuestStepXp { get; set; } = 20;

    public int QuestCompleteXp { get; set; } = 50;

    /// <summary>Market → IANA time zone, for "Today" / "Yesterday" in the history.</summary>
    public Dictionary<string, string> TimeZones { get; set; } = new(StringComparer.Ordinal)
    {
        ["IN"] = "Asia/Kolkata",
        ["ZA"] = "Africa/Johannesburg",
    };

    public IReadOnlyList<LadderStep> Ladder() => (Levels.Count > 0 ? Levels : LadderStep.Defaults).OrderBy(l => l.Min).ToList();

    public TimeZoneInfo TimeZoneFor(string market) =>
        TimeZoneInfo.FindSystemTimeZoneById(TimeZones.GetValueOrDefault(market, TimeZones.GetValueOrDefault("IN", "Asia/Kolkata")));
}

public sealed class CleanupOptions
{
    public bool Enabled { get; set; } = true;

    public TimeSpan Every { get; set; } = TimeSpan.FromHours(6);

    public int Batch { get; set; } = 1000;
}

public sealed class LadderStep
{
    public static readonly IReadOnlyList<LadderStep> Defaults =
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
