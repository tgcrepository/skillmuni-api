namespace Careers.Application;

/// <summary>Careers rules. Bound from the <c>Careers</c> config section; every value has a working default.</summary>
public sealed class CareersOptions
{
    public const string Section = "Careers";

    /// <summary>Market → IANA time zone for "today" (relative dates, fest countdowns, invitation expiry).</summary>
    public Dictionary<string, string> TimeZones { get; set; } = new(StringComparer.Ordinal)
    {
        ["IN"] = "Asia/Kolkata",
        ["ZA"] = "Africa/Johannesburg",
    };

    /// <summary>Hire Fest booking needs a Job Profile of at least this % (A-230, BR-68).</summary>
    public int FestBookingGate { get; set; } = 60;

    /// <summary>Matching lists jobs at this match % or more (A-222, BR-65).</summary>
    public int MatchingThreshold { get; set; } = 60;

    /// <summary>A withdrawn application can be sent again after this many days (BR-66).</summary>
    public int ReapplyCoolOffDays { get; set; } = 30;

    /// <summary>"Cancel anytime up to 24 hrs before" (fest cancel policy).</summary>
    public int CancelCutoffHours { get; set; } = 24;

    /// <summary>FR-42: CV upload limit.</summary>
    public long MaxCvBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Most items one list call returns (<c>limit</c>).</summary>
    public int MaxListLimit { get; set; } = 200;

    /// <summary>JobsSummary.minutesToUnlock (display estimate).</summary>
    public int MinutesToUnlock { get; set; } = 5;

    public string WaitlistOdds { get; set; } = "Usually 1 in 3 waitlisted learners get a slot";

    public MissionOptions Missions { get; set; } = new();

    public AssessmentCardOptions Assessment { get; set; } = new();

    public MatchingOptions Matching { get; set; } = new();

    public FestOptions Fest { get; set; } = new();

    public TimeZoneInfo TimeZoneFor(string market) =>
        TimeZoneInfo.FindSystemTimeZoneById(TimeZones.GetValueOrDefault(market, TimeZones.GetValueOrDefault("IN", "Asia/Kolkata")));
}

/// <summary>
/// Launchpad missions (BR-60, BR-61). XP here is display only: Gamification credits it from events (BR-110 to BR-112;
/// the assessment mission is the assessment's own 100, BR-104/105).
/// </summary>
public sealed class MissionOptions
{
    public int AssessmentXp { get; set; } = 100;

    public int ProfileXp { get; set; } = 50;

    public int ApplyXp { get; set; } = 100;

    public int FestXp { get; set; } = 100;

    public List<string> LevelNames { get; set; } = [];

    public IReadOnlyList<string> Levels() =>
        LevelNames.Count == 5 ? LevelNames : ["On the launchpad", "Engines warming up", "Lift-off", "Almost in orbit", "Launched!"];
}

public sealed class AssessmentCardOptions
{
    public int Questions { get; set; } = 36;

    public int Minutes { get; set; } = 8;

    /// <summary>Courses the report marks mandatory for the learner's path (shown once the assessment is done).</summary>
    public int MandatoryCourses { get; set; } = 2;
}

/// <summary>
/// Match % = skills × <see cref="SkillWeight"/> + location × <see cref="LocationWeight"/> + assessment ×
/// <see cref="AssessmentWeight"/>. A skill with proof (assessed, verified, language level) counts
/// <see cref="ProvenSkill"/>, a declared one <see cref="DeclaredSkill"/> (BRD §4.5, q7-receipts.ts).
/// </summary>
public sealed class MatchingOptions
{
    public double SkillWeight { get; set; } = 0.6;

    public double LocationWeight { get; set; } = 0.25;

    public double AssessmentWeight { get; set; } = 0.15;

    public double ProvenSkill { get; set; } = 1.0;

    public double DeclaredSkill { get; set; } = 0.8;

    /// <summary>My Matches preview radius.</summary>
    public int RadiusKm { get; set; } = 25;
}

/// <summary>BR-69 slot rules.</summary>
public sealed class FestOptions
{
    public int InterviewMinutes { get; set; } = 30;

    public int BufferMinutes { get; set; } = 30;

    public int MaxEmployers { get; set; } = 2;

    /// <summary>A day shows "few left" when the chosen employers have fewer seats than this.</summary>
    public int FewLeftBelow { get; set; } = 12;
}
