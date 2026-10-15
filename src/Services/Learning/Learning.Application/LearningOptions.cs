namespace Learning.Application;

/// <summary>Bound from the <c>Learning</c> config section; every value has a working default.</summary>
public sealed class LearningOptions
{
    public const string Section = "Learning";

    /// <summary>BR-107: what the first-moves card shows and the claim returns. Gamification credits the real XP.</summary>
    public int FirstMovesXp { get; set; } = 50;

    /// <summary>XP the "Discover your Career Drivers" first move offers (BR-104 + BR-105: 4 × 25).</summary>
    public int CareerDriversXp { get; set; } = 100;

    /// <summary>MISS-04: learning minutes counted per local day at most.</summary>
    public int MaxDailyMinutes { get; set; } = 1440;

    /// <summary>My Stats "finish-line nudge" for an in-progress module at or above this percentage.</summary>
    public double NudgeAtPercent { get; set; } = 90;

    /// <summary>Market → IANA time zone for the greeting and day boundaries.</summary>
    public Dictionary<string, string> TimeZones { get; set; } = new(StringComparer.Ordinal)
    {
        ["IN"] = "Asia/Kolkata",
        ["ZA"] = "Africa/Johannesburg",
    };

    public TimeZoneInfo TimeZoneFor(string market) =>
        TimeZoneInfo.FindSystemTimeZoneById(TimeZones.GetValueOrDefault(market, TimeZones.GetValueOrDefault("IN", "Asia/Kolkata")));
}
