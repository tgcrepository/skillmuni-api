using Careers.Application;
using Careers.Application.Abstractions;
using Careers.Application.Services;
using Careers.Domain;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Careers.UnitTests;

public sealed class MatcherAndListTests
{
    private static readonly string[] CsSkills = ["Communication", "Customer service", "English", "Hindi", "CRM", "MS Office"];

    private readonly JobMatcher _matcher = new(MsOptions.Create(new CareersOptions()));

    [Fact]
    public void Proof_counts_more_than_a_declared_skill()
    {
        var proven = _matcher.Score(["Communication", "English"], "Pune", "Maharashtra", false, Profile(new LearnerSkill("Communication", "verified"), new LearnerSkill("English", "assessed")));
        var declared = _matcher.Score(["Communication", "English"], "Pune", "Maharashtra", false, Profile(new LearnerSkill("Communication", "declared"), new LearnerSkill("English", "declared")));

        // 0.6 × 1.0 + 0.25 × 1 (Pune) + 0.15 × 0.5 (no assessment) = 92.5 → 93; declared: 0.6 × 0.8 → 80.5 → 81.
        Assert.Equal(93, proven.Percent);
        Assert.Equal(81, declared.Percent);
        Assert.Null(proven.Info.Gap);
        Assert.Contains(proven.Info.Reasons, r => r.Text == "Location: Pune, where you live");
    }

    [Fact]
    public void Missing_skills_show_as_the_gap()
    {
        var result = _matcher.Score(CsSkills, "Mumbai", "Maharashtra", false, Profile(new LearnerSkill("Communication", "verified"), new LearnerSkill("customer service", "declared")));

        Assert.Equal(new[] { "Communication", "Customer service" }, result.Info.Skills);
        Assert.Equal("Skills: 2 of 6 match (communication, customer service)", result.Info.Reasons[0].Text);
        Assert.False(result.Info.Reasons[0].Ok);
        Assert.Equal("Location: Mumbai, in Maharashtra", result.Info.Reasons[1].Text);
        Assert.Equal("Missing: English", result.Info.Reasons[^1].Text);
        Assert.Equal("Close the gap: English basics", result.Info.Gap!.Label);
    }

    [Fact]
    public void Remote_jobs_suit_everyone_and_the_assessment_lifts_the_match()
    {
        var before = _matcher.Score(["Typing"], "Remote", string.Empty, true, Profile(new LearnerSkill("Typing", "verified")));
        var after = _matcher.Score(["Typing"], "Remote", string.Empty, true, Profile(new LearnerSkill("Typing", "verified")) with { Archetype = "The Specialist Operator" });

        Assert.Equal(93, before.Percent);
        Assert.Equal(100, after.Percent);
        Assert.Contains(after.Info.Reasons, r => r.Text == "Your assessment: The Specialist Operator");
    }

    [Fact]
    public void A_language_pair_covers_each_language()
    {
        Assert.True(JobMatcher.SameSkill("Hindi + English", "Hindi"));
        Assert.True(JobMatcher.SameSkill("MS Excel", "ms excel"));
        Assert.False(JobMatcher.SameSkill("IT", "Retail"));
    }

    [Fact]
    public void Filters_are_and_across_keys_and_or_within_one()
    {
        var items = new[] { ("a", "Pune", "onsite"), ("b", "Mumbai", "onsite"), ("c", "Remote", "remote") };
        IReadOnlyList<string>? Get((string Id, string City, string Mode) x, string key) => key switch
        {
            "city" => x.Mode == "remote" ? [] : [x.City],
            "workMode" => [x.Mode],
            _ => null,
        };

        var cityOnly = ListEngine.ParseQuery(null, null, null, null, """{"city":["Pune","Mumbai"]}""", 200);
        Assert.Equal(2, items.Count(i => ListEngine.Passes(i, cityOnly.Filters, Get)));

        var withRemote = ListEngine.ParseQuery(null, null, null, null, """{"city":["Pune"],"remote":["yes"]}""", 200);
        Assert.Equal(new[] { "a", "c" }, items.Where(i => ListEngine.Passes(i, withRemote.Filters, Get)).Select(i => i.Item1));

        var none = ListEngine.ParseQuery(null, null, null, null, """{"city":["Pune"],"workMode":["hybrid"]}""", 200);
        Assert.DoesNotContain(items, i => ListEngine.Passes(i, none.Filters, Get));
        var suggestions = ListEngine.Suggestions(items, none.Filters, Get);
        var only = Assert.Single(suggestions);
        Assert.Equal(("workMode", "hybrid", 1), (only.Key, only.Id, only.Count));
    }

    [Fact]
    public void Query_parsing_ignores_bad_filters_and_clamps_the_limit()
    {
        var bad = ListEngine.ParseQuery("cs", "Pune", "newest", 5000, "{not json", 200);
        Assert.Empty(bad.Filters);
        Assert.Equal(200, bad.Limit);
        Assert.Equal("cs", bad.Q);
        Assert.Equal(20, ListEngine.ParseQuery(null, null, null, null, null, 200).Limit);
    }

    [Fact]
    public void Timeline_follows_the_mock()
    {
        var applied = JobBoard.Timeline(ApplicationStatus.Applied, ["2 Oct"]);
        Assert.Equal(new[] { "done", "todo", "todo", "todo", "todo" }, applied.Select(s => s.State));
        Assert.Equal("2 Oct", applied[0].Date);

        var interview = JobBoard.Timeline(ApplicationStatus.Interview, ["24 Sep", "26 Sep", "28 Sep", "12 Oct"]);
        Assert.Equal(new[] { "done", "done", "done", "current", "todo" }, interview.Select(s => s.State));

        var withdrawn = JobBoard.Timeline(ApplicationStatus.Withdrawn, ["2 Sep", "8 Sep"]);
        Assert.Equal(new[] { "Applied", "Withdrawn" }, withdrawn.Select(s => s.Label));
        Assert.Equal("failed", withdrawn[1].State);
    }

    private static MatchProfile Profile(params LearnerSkill[] skills) => new(skills, "Pune", "Maharashtra", false, null);
}
