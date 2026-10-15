using Careers.Application.Abstractions;
using Careers.Application.Contracts;
using Microsoft.Extensions.Options;

namespace Careers.Application.Services;

/// <summary>What the matcher knows about the learner. Null skills = unknown (no match is shown).</summary>
public sealed record MatchProfile(IReadOnlyList<LearnerSkill> Skills, string? City, string? State, bool? Relocate, string? Archetype)
{
    public bool AssessmentDone => Archetype is not null;
}

public sealed record MatchedSkill(string Name, string Evidence);

public sealed record MatchResult(int Percent, IReadOnlyList<MatchedSkill> Matched, IReadOnlyList<string> Missing, MatchInfoDto Info);

/// <summary>
/// Scores a job for a learner (BR-65, BRD §4.5). Skills carry most of the weight, each counted by its evidence
/// (proof 1.0, declared 0.8); then location, then whether the assessment is done. Weights are in
/// <see cref="MatchingOptions"/>. Pure and deterministic.
/// </summary>
public sealed class JobMatcher(IOptions<CareersOptions> options)
{
    private const int MinContains = 3;

    private MatchingOptions Rules => options.Value.Matching;

    public MatchResult Score(IReadOnlyList<string> jobSkills, string city, string state, bool remote, MatchProfile profile)
    {
        var matched = new List<MatchedSkill>();
        var missing = new List<string>();
        var weight = 0.0;
        foreach (var skill in jobSkills)
        {
            var best = profile.Skills
                .Where(s => SameSkill(s.Name, skill))
                .Select(s => (s.Evidence, Weight: EvidenceWeight(s.Evidence)))
                .OrderByDescending(s => s.Weight)
                .FirstOrDefault();
            if (best.Evidence is null)
            {
                missing.Add(skill);
                continue;
            }

            matched.Add(new MatchedSkill(skill, best.Evidence));
            weight += best.Weight;
        }

        var skillScore = jobSkills.Count == 0 ? 1.0 : weight / jobSkills.Count;
        var (locationScore, locationReason) = Location(city, state, remote, profile);
        var assessmentScore = profile.AssessmentDone ? 1.0 : 0.5;
        var raw = (Rules.SkillWeight * skillScore) + (Rules.LocationWeight * locationScore) + (Rules.AssessmentWeight * assessmentScore);
        var percent = (int)Math.Clamp(Math.Round(raw * 100, MidpointRounding.AwayFromZero), 0, 100);

        var reasons = new List<MatchReasonDto>
        {
            new(
                matched.Count * 2 >= jobSkills.Count,
                matched.Count == 0
                    ? $"Skills: 0 of {jobSkills.Count} match"
                    : $"Skills: {matched.Count} of {jobSkills.Count} match ({string.Join(", ", matched.Select(m => m.Name.ToLowerInvariant()))})"),
        };
        if (locationReason is not null)
        {
            reasons.Add(locationReason);
        }

        if (profile.Archetype is { } archetype)
        {
            reasons.Add(new MatchReasonDto(true, $"Your assessment: {archetype}"));
        }

        MatchGapDto? gap = null;
        if (missing.Count > 0)
        {
            reasons.Add(new MatchReasonDto(false, $"Missing: {missing[0]}"));
            gap = new MatchGapDto($"Close the gap: {missing[0]} basics", "/learn");
        }

        var info = new MatchInfoDto(percent, matched.Take(3).Select(m => m.Name).ToList(), reasons, gap);
        return new MatchResult(percent, matched, missing, info);
    }

    /// <summary>How much adding proof for a declared skill would lift the match (whole %).</summary>
    public int ProofBoost(int jobSkillCount) =>
        jobSkillCount == 0 ? 0 : (int)Math.Round(100 * Rules.SkillWeight * (Rules.ProvenSkill - Rules.DeclaredSkill) / jobSkillCount, MidpointRounding.AwayFromZero);

    public double EvidenceWeight(string evidence) => evidence == "declared" ? Rules.DeclaredSkill : Rules.ProvenSkill;

    /// <summary>Case-insensitive; a longer name containing the other counts ("Hindi + English" covers "Hindi").</summary>
    internal static bool SameSkill(string learner, string job)
    {
        var a = learner.Trim();
        var b = job.Trim();
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Math.Min(a.Length, b.Length) >= MinContains
            && (a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase));
    }

    private static (double Score, MatchReasonDto? Reason) Location(string city, string state, bool remote, MatchProfile profile)
    {
        if (remote)
        {
            return (1.0, new MatchReasonDto(true, "Location: remote, work from anywhere"));
        }

        if (string.IsNullOrWhiteSpace(profile.City))
        {
            return (0.5, null);
        }

        if (city.Equals(profile.City, StringComparison.OrdinalIgnoreCase))
        {
            return (1.0, new MatchReasonDto(true, $"Location: {city}, where you live"));
        }

        if (profile.Relocate == true)
        {
            return (0.8, new MatchReasonDto(true, $"Location: {city}, you said you’d relocate"));
        }

        if (!string.IsNullOrWhiteSpace(profile.State) && state.Equals(profile.State, StringComparison.OrdinalIgnoreCase))
        {
            return (0.6, new MatchReasonDto(true, $"Location: {city}, in {state}"));
        }

        return (0.2, new MatchReasonDto(false, $"Location: {city}, outside your area"));
    }
}
