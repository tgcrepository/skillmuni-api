namespace Assessment.Application;

/// <summary>Bound from the <c>Assessment</c> config section; every value has a working default.</summary>
public sealed class AssessmentOptions
{
    public const string Section = "Assessment";

    /// <summary>
    /// XP shown when islands 1–3 are cleared (BR-104), first attempt only. Display copy: Gamification credits the real
    /// XP from <c>assessment.island.cleared</c> and must use the same amount.
    /// </summary>
    public int XpPerIsland { get; set; } = 25;

    /// <summary>XP shown on submit for the last island (BR-105, A-309): 100 in all on the first attempt.</summary>
    public int XpOnSubmit { get; set; } = 25;

    /// <summary>"N mandatory courses for your path" on the results (mock constant; Learning will own it).</summary>
    public int MandatoryCourses { get; set; } = 2;

    /// <summary>Market → IANA time zone for the attempt date.</summary>
    public Dictionary<string, string> TimeZones { get; set; } = new(StringComparer.Ordinal)
    {
        ["IN"] = "Asia/Kolkata",
        ["ZA"] = "Africa/Johannesburg",
    };

    /// <summary>My Stats → Assessments (API-131).</summary>
    public SkillStatsOptions Stats { get; set; } = new();

    public TimeZoneInfo TimeZoneFor(string market) =>
        TimeZoneInfo.FindSystemTimeZoneById(TimeZones.GetValueOrDefault(market, TimeZones.GetValueOrDefault("IN", "Asia/Kolkata")));
}

/// <summary>
/// The Skill Stats slide lists scored skill assessments (Digital Skills, Leadership…). Those tests aren't part of
/// Phase 1 yet, so the slide shows its empty state (Figma 161:4514) with these values from the mock.
/// </summary>
public sealed class SkillStatsOptions
{
    public int Available { get; set; } = 20;

    /// <summary>Shown on the empty state's first card. Display only; no rule in BRD §4.1 pays it yet.</summary>
    public int FirstCardXp { get; set; } = 20;

    public string RecommendedTitle { get; set; } = "Digital Skills";

    public string RecommendedUrl { get; set; } = "/assessments/digital-skills";

    public string RecommendedWhy { get; set; } = "Start with Digital Skills. Most of your batch unlocks it first.";
}
