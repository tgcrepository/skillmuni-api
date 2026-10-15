using System.Text.Json;
using Assessment.Application;
using Assessment.Domain;
using Assessment.Infrastructure.Content;

namespace Assessment.UnitTests;

public sealed class ContentTests
{
    private static CareerDriversContent Content => AssessmentHarness.Content.Content;

    [Fact]
    public void Survey_has_36_pairs_4_islands_9_drivers_and_6_archetypes()
    {
        Assert.Equal(36, Content.Pairs.Count);
        Assert.Equal(new[] { (1, 1, 9), (2, 10, 18), (3, 19, 27), (4, 28, 36) }, Content.Islands.Select(i => (i.N, i.From, i.To)));
        Assert.Equal(Drivers.All, Content.Drivers.Select(d => d.Id));
        Assert.Equal(
            ["people-first", "rainmaker", "specialist", "pathfinder", "purpose", "strategist"],
            Content.Archetypes.Select(a => a.Id));
        Assert.Equal(4, Content.LastIsland);
    }

    [Fact]
    public void Each_driver_appears_in_8_pairs()
    {
        var counts = Content.Pairs.SelectMany(p => new[] { p.This.Driver, p.That.Driver }).GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());
        Assert.All(Drivers.All, d => Assert.Equal(8, counts[d]));
    }

    [Fact]
    public void Text_is_verbatim_from_the_survey()
    {
        Assert.Equal("I aim for a lifestyle where I can comfortably afford the experiences and things I want.", Content.Pairs[0].This.Text);
        Assert.Equal("It’s important for me to work in a supportive, collaborative environment.", Content.Pairs[2].That.Text);
        Assert.Equal(new[] { "D", "G" }, Content.FindArchetype("strategist")!.EitherOf);
        Assert.Null(Content.FindArchetype("rainmaker")!.EitherOf);
        Assert.Equal("Vibe Check", Content.Islands[0].Name);
    }

    [Fact]
    public void Broken_content_fails_validation()
    {
        var broken = Content with { Pairs = [new Pair(1, new Statement("x", "Z"), new Statement("y", "A"))] };
        Assert.Throws<InvalidOperationException>(() => EmbeddedCareerDriversContent.Validate(broken));
    }
}

public sealed class ScoringTests
{
    private static CareerDriversContent Content => AssessmentHarness.Content.Content;

    private static DriverScores Scores(int a, int b, int c, int d, int e, int f, int g, int h, int i) => new([a, b, c, d, e, f, g, h, i]);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Each_pair_splits_3_points_so_all_36_make_108(int notch)
    {
        var scores = CareerDriversScoring.Score(Content.Pairs, AssessmentHarness.All(notch));
        Assert.Equal(108, scores.Total);
        Assert.All(scores.Values, v => Assert.InRange(v, 0, 24));
    }

    [Fact]
    public void Notch_0_puts_all_three_coins_on_this()
    {
        var answers = Answer.Empty(36).ToList();
        answers[0] = Answer.FromNotch(0); // A vs B
        answers[1] = Answer.FromNotch(3); // C vs D
        answers[2] = Answer.FromNotch(1); // E vs F
        answers[3] = Answer.Skip;
        var scores = CareerDriversScoring.Score(Content.Pairs, answers);
        Assert.Equal(Scores(3, 0, 0, 3, 2, 1, 0, 0, 0), scores);
    }

    [Fact]
    public void Ranking_is_highest_first_and_ties_keep_survey_order()
    {
        var ranking = CareerDriversScoring.Rank(Scores(10, 10, 12, 9, 14, 12, 14, 13, 14));
        Assert.Equal(new[] { "E", "G", "I", "H", "C", "F", "A", "B", "D" }, ranking);
    }

    [Fact]
    public void Archetype_is_the_highest_dominant_pair()
    {
        // The Figma S1 example (attempt 1 in the mock): E + G = 28.
        Assert.Equal("pathfinder", CareerDriversScoring.Match(Scores(10, 10, 12, 9, 14, 12, 14, 13, 14), Content.Archetypes).Id);
        Assert.Equal("people-first", CareerDriversScoring.Match(Scores(0, 0, 0, 0, 0, 20, 0, 20, 0), Content.Archetypes).Id);
    }

    [Fact]
    public void Dominant_ties_go_to_the_higher_secondary_average_then_table_order()
    {
        // Rainmaker (A+I) and Pathfinder (E+G) both 20; Pathfinder's secondaries I, A average 10, Rainmaker's B, G only 5.
        Assert.Equal("pathfinder", CareerDriversScoring.Match(Scores(10, 0, 0, 0, 10, 0, 10, 0, 10), Content.Archetypes).Id);

        // Everything equal: table order.
        Assert.Equal("people-first", CareerDriversScoring.Match(Scores(12, 12, 12, 12, 12, 12, 12, 12, 12), Content.Archetypes).Id);
    }

    [Fact]
    public void Strategic_leader_counts_the_stronger_of_d_or_g()
    {
        // Strategist (B+I = 20) ties Rainmaker (A+I = 20). Strategist's secondaries: A and max(D, G) = 16 → 13;
        // Rainmaker's B and G → (10 + 16) / 2 = 13 too, so the table order decides... unless D lifts the strategist.
        Assert.Equal("rainmaker", CareerDriversScoring.Match(Scores(10, 10, 0, 0, 0, 0, 16, 0, 10), Content.Archetypes).Id);
        Assert.Equal("strategist", CareerDriversScoring.Match(Scores(10, 10, 0, 18, 0, 0, 16, 0, 10), Content.Archetypes).Id);
    }

    [Fact]
    public void Answers_round_trip_through_their_storage_code()
    {
        IReadOnlyList<Answer> answers = [Answer.FromNotch(0), Answer.FromNotch(3), Answer.Skip, Answer.None];
        Assert.Equal("03s-", Answer.Encode(answers));
        Assert.Equal(answers, Answer.Decode("03s-", 4));
        Assert.Equal(new[] { Answer.FromNotch(0), Answer.None }, Answer.Decode("0", 2));
        Assert.True(default(Answer).IsNone);
    }
}

public sealed class AnswerJsonTests
{
    private static readonly JsonSerializerOptions Web = JsonSerializerOptions.Web;

    [Fact]
    public void Answers_use_the_frontend_union()
    {
        var request = JsonSerializer.Deserialize<SaveAnswersRequest>("""{"answers":[0,3,"skip",null],"current":2}""", Web)!;
        Assert.Equal(new[] { Answer.FromNotch(0), Answer.FromNotch(3), Answer.Skip, Answer.None }, request.Answers);
        Assert.Equal(2, request.Current);

        var json = JsonSerializer.Serialize(new SubmitAnswersRequest(request.Answers), Web);
        Assert.Equal("""{"answers":[0,3,"skip",null]}""", json);
    }

    [Theory]
    [InlineData("""{"answers":[4]}""")]
    [InlineData("""{"answers":["later"]}""")]
    [InlineData("""{"answers":{"a":1}}""")]
    [InlineData("""{"answers":[1.5]}""")]
    public void Anything_else_is_malformed(string body) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SubmitAnswersRequest>(body, Web));

    [Fact]
    public void Either_of_is_left_out_when_an_archetype_has_none()
    {
        var dto = new ArchetypeDto("x", "X", "🙂", ["A"], ["B"], null, "r", "f", "s");
        Assert.DoesNotContain("eitherOf", JsonSerializer.Serialize(dto, Web), StringComparison.Ordinal);
    }
}
