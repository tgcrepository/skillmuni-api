namespace Assessment.Domain;

// Career Drivers Survey 2025 (client document, 2026-10-06): the content the server owns. Shapes follow the frontend's
// assessment.api.ts so they serialize unchanged.

/// <summary>The 9 Career Drivers, A–I, in survey order (BR-51). Ties always rank in this order.</summary>
public static class Drivers
{
    public static readonly IReadOnlyList<string> All = ["A", "B", "C", "D", "E", "F", "G", "H", "I"];

    public static int IndexOf(string driver) =>
        driver.Length == 1 && driver[0] is >= 'A' and <= 'I' ? driver[0] - 'A' : throw new ArgumentOutOfRangeException(nameof(driver), driver, "Drivers are A to I.");

    public static bool IsDriver(string? driver) => driver is { Length: 1 } && driver[0] is >= 'A' and <= 'I';
}

/// <summary>One side of a pair. <see cref="Driver"/> is hidden from the learner until the reveal (anti-bias).</summary>
public sealed record Statement(string Text, string Driver);

/// <summary>A "this or that" pair; <see cref="N"/> is 1-based.</summary>
public sealed record Pair(int N, Statement This, Statement That);

/// <summary>A group of pairs on the quest map (FR-30). <see cref="From"/> and <see cref="To"/> are 1-based, inclusive.</summary>
public sealed record Island(int N, string Name, string Emoji, int From, int To)
{
    public bool IsComplete(IReadOnlyList<Answer> answers) =>
        Enumerable.Range(From - 1, To - From + 1).All(i => i < answers.Count && answers[i].IsAnswered);
}

public sealed record DriverInfo(
    string Id,
    string Name,
    string Emoji,
    string Focus,
    string Definition,
    string Blurb,
    string StrengthZones,
    string Families);

/// <param name="EitherOf">"± D or G": the stronger of these counts as one more secondary driver.</param>
public sealed record Archetype(
    string Id,
    string Name,
    string Emoji,
    IReadOnlyList<string> Dominant,
    IReadOnlyList<string> Secondary,
    IReadOnlyList<string>? EitherOf,
    string Roles,
    string RedFlags,
    string Summary);

/// <summary>Everything the assessment shows and scores against.</summary>
public sealed record CareerDriversContent(
    IReadOnlyList<Pair> Pairs,
    IReadOnlyList<Island> Islands,
    IReadOnlyList<DriverInfo> Drivers,
    IReadOnlyList<Archetype> Archetypes)
{
    /// <summary>The last island is paid on submit, not when its last pair is answered (A-309).</summary>
    public int LastIsland => Islands.Count == 0 ? 0 : Islands.Max(i => i.N);

    public Archetype? FindArchetype(string? id) => Archetypes.FirstOrDefault(a => a.Id == id);

    public DriverInfo? FindDriver(string id) => Drivers.FirstOrDefault(d => d.Id == id);
}
