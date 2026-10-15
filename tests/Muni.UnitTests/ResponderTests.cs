using Muni.Application;
using Muni.Application.Responders;

namespace Muni.UnitTests;

public sealed class ResponderTests
{
    private readonly MuniHarness _muni = new();

    private async Task<MuniAnswer> AskAsync(string text, string? intent = null) =>
        await _muni.Responder.AnswerAsync(new MuniQuestion(text, intent), await _muni.MeAsync(), CancellationToken.None);

    [Fact]
    public async Task Topic_tile_opens_its_chips()
    {
        var answer = await AskAsync("Jobs", "jobs");

        Assert.Equal("Jobs it is 💼 What do you want to do?", Assert.IsType<TextBlock>(Assert.Single(answer.Blocks)).Text);
        Assert.Equal(new[] { "Jobs for me", "Applications", "Career Compass" }, answer.Chips);
    }

    [Fact]
    public async Task Greeting_is_personal()
    {
        _muni.Known(xp: 120);

        var answer = await AskAsync("hey there");

        Assert.Equal("greeting", answer.Topic);
        Assert.Equal(
            "Heyyy Yash 👋 You’re Level 1 · Rookie with 120 XP. Ask me anything about Skillmuni: XP, jobs, your profile, Hire Fest, the Arena…",
            Assert.IsType<TextBlock>(answer.Blocks[0]).Text);
    }

    [Theory]
    [InlineData("how do i get xp?", "xp")]
    [InlineData("Earn points", "xp")]
    [InlineData("what are my badges", "badges")]
    [InlineData("is my aadhaar safe", "id")]
    [InlineData("How do streaks work?", "streak")]
    [InlineData("What's Hire Fest?", "fest")]
    [InlineData("What can you do?", "muni")]
    public async Task Free_questions_are_keyword_scored(string question, string topic)
    {
        Assert.Equal(topic, (await AskAsync(question)).Topic);
    }

    [Fact]
    public async Task Unknown_question_falls_back()
    {
        var answer = await AskAsync("quantum chromodynamics");

        Assert.Equal("fallback", answer.Topic);
        Assert.StartsWith("Hmm, I don’t have that one yet, Yash", Assert.IsType<TextBlock>(answer.Blocks[0]).Text);
    }

    [Fact]
    public async Task Xp_answer_uses_the_learners_numbers()
    {
        _muni.Known(xp: 150);

        var answer = await AskAsync("how do i earn xp");

        var first = Assert.IsType<TextBlock>(answer.Blocks[0]).Text;
        Assert.Contains("You’ve got 150 XP right now, Level 1 · Rookie, 250 XP to the next level.", first, StringComparison.Ordinal);
        Assert.Contains("+20 XP for every step, +50 XP when a quest is done", Assert.IsType<TextBlock>(answer.Blocks[1]).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Profile_question_lists_gaps_from_learner()
    {
        var answer = await AskAsync("what is missing in my profile");

        Assert.Equal("profile", answer.Topic);
        var list = Assert.IsType<ListBlock>(answer.Blocks[1]);
        var gap = Assert.Single(list.Items);
        Assert.Equal("Your Flex Era", gap.Title);
        Assert.Equal("Left: Languages, Hobbies", gap.Detail);
        Assert.Equal("/quests/your-flex-era?part=3", gap.Url);
        Assert.Equal("Fill", gap.Tag);
    }

    [Fact]
    public async Task Profile_chip_masks_private_fields()
    {
        _muni.Known();

        var answer = await AskAsync("Show my profile", "Show my profile");

        var card = Assert.IsType<ProfileBlock>(answer.Blocks[1]);
        Assert.Equal("YP", card.Initials);
        Assert.Equal("Yash Pohare", card.Name);
        Assert.Equal("Rookie · Lvl 1", card.Subtitle);
        Assert.Equal("y••••@thegamification.com", card.Fields[0].Value);
        Assert.True(card.Fields[0].Masked);
        Assert.Equal("••••••0697", card.Fields[1].Value);
        Assert.Equal("Apr 2025", card.Fields[2].Value);
        Assert.False(card.Fields[2].Masked);
        Assert.Equal(50, card.Completeness);
        Assert.Equal("Next: Your Flex Era · Languages, Hobbies", card.NextStep);
    }

    [Fact]
    public async Task Profile_chip_survives_learner_being_down()
    {
        _muni.Store.Private[RevealFields.Email] = null;
        _muni.Store.Private[RevealFields.Mobile] = string.Empty;

        var card = Assert.IsType<ProfileBlock>((await AskAsync("Show my profile", "Show my profile")).Blocks[1]);

        Assert.Equal("••••••", card.Fields[0].Value);
        Assert.Equal("Not added", card.Fields[1].Value);
        Assert.False(card.Fields[1].Masked);
    }

    [Fact]
    public async Task Xp_chip_shows_stats()
    {
        _muni.Known(xp: 100);

        var answer = await AskAsync("My XP & rank", "My XP & rank");

        var stats = Assert.IsType<StatsBlock>(answer.Blocks[1]);
        Assert.Equal(new[] { "100", "1 · Rookie", "300" }, stats.Items.Select(i => i.Value));
        Assert.Equal("/arena", stats.Link.Url);
    }

    [Fact]
    public async Task Career_chip_without_drivers_invites_the_assessment()
    {
        var answer = await AskAsync("Take the Career assessment", "Take the Career assessment");

        Assert.Equal("chip:archetype", answer.Topic);
        Assert.StartsWith("You haven’t taken Career Drivers yet.", Assert.IsType<TextBlock>(answer.Blocks[0]).Text);
    }

    [Theory]
    [InlineData("yash.p@gmail.com", "y••••@gmail.com")]
    [InlineData("a@b.co", "a••••@b.co")]
    public void Email_mask_matches_the_mock(string email, string masked) => Assert.Equal(masked, KeywordMuniResponder.MaskEmail(email));

    [Fact]
    public void Mobile_mask_keeps_last_four_digits() => Assert.Equal("••••••0697", KeywordMuniResponder.MaskMobile("+91 98765 40697"));

    [Fact]
    public void Words_strip_punctuation() => Assert.Equal("what s hire fest", MuniKnowledge.Words("What’s   Hire-Fest?"));
}
