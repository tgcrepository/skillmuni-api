using Muni.Application;
using Muni.Application.Handlers;
using Muni.Application.Services;
using Muni.Domain;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Muni.UnitTests;

public sealed class MuniServiceTests
{
    private readonly MuniHarness _muni = new();

    [Fact]
    public async Task Welcome_for_a_new_learner_starts_with_discovery()
    {
        var welcome = await _muni.Service.WelcomeAsync(_muni.Who, CancellationToken.None);

        Assert.Equal("Yash", welcome.FirstName);
        Assert.Equal("Level 1 · Rookie, 0 XP. Ask me anything about Skillmuni, or start with what I’d do right now:", welcome.Intro);
        Assert.Equal(new[] { "drivers", "learn", "jobs" }, welcome.Suggestions.Select(s => s.Id));
        Assert.Equal("Jobs for me", welcome.Suggestions[2].Intent);
        Assert.Null(welcome.Suggestions[2].Link);
        Assert.Equal(new[] { "jobs", "learn", "assess", "me" }, welcome.Topics.Select(t => t.Id));
        Assert.Equal(0, welcome.Unread);
        Assert.Null(welcome.Nudge);
    }

    [Fact]
    public async Task Welcome_for_a_known_learner_keeps_the_streak()
    {
        _muni.Known(xp: 410, streak: 3);

        var welcome = await _muni.Service.WelcomeAsync(_muni.Who, CancellationToken.None);

        Assert.Equal(new[] { "finish", "jobs", "streak" }, welcome.Suggestions.Select(s => s.Id));
        Assert.Equal("Keep your 3-day streak", welcome.Suggestions[2].Title);
    }

    [Fact]
    public async Task Ask_stores_the_exchange_in_one_conversation()
    {
        var first = await _muni.Service.AskAsync(_muni.Who, new AskRequest("hi", null), CancellationToken.None);
        _muni.Clock.Advance(TimeSpan.FromMinutes(5));
        await _muni.Service.AskAsync(_muni.Who, new AskRequest("Jobs for me", "Jobs for me"), CancellationToken.None);

        Assert.Equal(4, _muni.Store.Messages.Count);
        Assert.Single(_muni.Store.Messages.Select(m => m.ConversationId).Distinct());
        var reply = _muni.Store.Messages[1];
        Assert.Equal(Speakers.Muni, reply.From);
        Assert.Equal(first.Id, reply.Id.ToString());
        Assert.Equal("greeting", reply.Topic);
        Assert.Contains("\"kind\":\"text\"", reply.ReplyJson, StringComparison.Ordinal);
        Assert.True(first.Chips.Count <= 3);
    }

    [Fact]
    public async Task A_long_pause_starts_a_new_conversation()
    {
        await _muni.Service.AskAsync(_muni.Who, new AskRequest("hi", null), CancellationToken.None);
        _muni.Clock.Advance(TimeSpan.FromHours(2));
        await _muni.Service.AskAsync(_muni.Who, new AskRequest("how do i earn xp", null), CancellationToken.None);

        var history = await _muni.Service.HistoryAsync(_muni.Who, CancellationToken.None);

        Assert.Equal(2, history.Count);
        Assert.Equal("how do i earn xp", history[0].Title);
        Assert.Equal("Today", history[0].When);
        Assert.Equal(new[] { "me", "muni" }, history[0].Messages.Select(m => m.From));
        Assert.StartsWith("XP is how Skillmuni keeps score", history[0].Messages[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task History_leaves_out_lines_past_retention()
    {
        await _muni.Service.AskAsync(_muni.Who, new AskRequest("hi", null), CancellationToken.None);
        _muni.Clock.Advance(TimeSpan.FromDays(_muni.Options.HistoryDays + 1));

        Assert.Empty(await _muni.Service.HistoryAsync(_muni.Who, CancellationToken.None));
    }

    [Theory]
    [InlineData(0, "Today")]
    [InlineData(1, "Yesterday")]
    [InlineData(2, "Tue")]
    [InlineData(9, "6 Oct")]
    public void When_labels(int daysAgo, string label)
    {
        var today = new DateOnly(2026, 10, 15);
        Assert.Equal(label, MuniService.When(today.AddDays(-daysAgo), today));
    }

    [Fact]
    public void Long_titles_are_clipped()
    {
        var text = new string('a', 80);
        var title = MuniService.Title([new ChatLine(Guid.NewGuid(), Guid.NewGuid(), Speakers.Learner, text, DateTime.UtcNow)]);
        Assert.Equal(MuniService.MaxTitleLength, title.Length);
        Assert.EndsWith("…", title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rating_is_set_changed_and_cleared()
    {
        Assert.True((await _muni.Service.RateAsync(_muni.Who, "m-1", true, CancellationToken.None)).IsSuccess);
        Assert.True(Assert.Single(_muni.Store.Ratings).Helpful);

        await _muni.Service.RateAsync(_muni.Who, "m-1", false, CancellationToken.None);
        Assert.False(Assert.Single(_muni.Store.Ratings).Helpful);

        await _muni.Service.RateAsync(_muni.Who, "m-1", null, CancellationToken.None);
        Assert.Empty(_muni.Store.Ratings);

        Assert.True((await _muni.Service.RateAsync(_muni.Who, "m-2", null, CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task Rating_rejects_an_oversized_id()
    {
        var result = await _muni.Service.RateAsync(_muni.Who, new string('x', ReplyRating.MaxRefLength + 1), true, CancellationToken.None);

        Assert.Equal(MuniErrors.InvalidMessageId, result.Error);
    }

    [Fact]
    public async Task Reveal_returns_the_value_or_503()
    {
        Assert.Equal("+91 98765 40697", (await _muni.Service.RevealAsync(_muni.Who, RevealFields.Mobile, CancellationToken.None)).Value.Value);

        _muni.Store.Private[RevealFields.Email] = null;
        Assert.Equal(MuniErrors.RevealUnavailable, (await _muni.Service.RevealAsync(_muni.Who, RevealFields.Email, CancellationToken.None)).Error);
    }

    [Fact]
    public async Task Snapshot_without_learner_data_uses_defaults()
    {
        _muni.Store.Progress = null;

        var me = await _muni.MeAsync();

        Assert.Equal("Yash", me.First);
        Assert.Equal("Y", me.Initials);
        Assert.True(me.Fresh);
        Assert.Equal(400, me.ToNext);
        Assert.Equal(0, me.ProfilePercent);
        Assert.Empty(me.Gaps);
    }

    [Fact]
    public async Task Events_build_the_learner_copy()
    {
        var accessor = new MuniLearnerAccessor(_muni.Store, _muni.Store, _muni.Clock, MsOptions.Create(_muni.Options));
        var id = MuniHarness.LearnerId;

        await new LearnerRegisteredHandler(accessor, _muni.Clock).HandleAsync(new LearnerRegistered(id, "Asha", "Rao", "IN") { TenantId = "skillmuni-in" }, CancellationToken.None);
        await new XpAwardedHandler(accessor, _muni.Clock).HandleAsync(new XpAwarded(id, 450, 2, "Pathfinder", 400, 800, 1, 4) { TenantId = "skillmuni-in" }, CancellationToken.None);
        await new XpAwardedHandler(accessor, _muni.Clock).HandleAsync(new XpAwarded(id, 300, 1, "Rookie", 0, 400, 1, 1) { TenantId = "skillmuni-in" }, CancellationToken.None);
        await new QuestCompletedHandler(accessor, _muni.Clock).HandleAsync(new QuestCompleted(id, "who-dis", 1, "ID Unlocked") { TenantId = "skillmuni-in" }, CancellationToken.None);
        await new QuestCompletedHandler(accessor, _muni.Clock).HandleAsync(new QuestCompleted(id, "who-dis", 1, "ID Unlocked") { TenantId = "skillmuni-in" }, CancellationToken.None);
        await new QuestCompletedHandler(accessor, _muni.Clock).HandleAsync(new QuestCompleted(id, "hit-me-up", 2, "Signal Boost") { TenantId = "skillmuni-in" }, CancellationToken.None);
        await new AssessmentSubmittedHandler(accessor, _muni.Clock).HandleAsync(new AssessmentSubmitted(id, 1, "The Connector") { TenantId = "skillmuni-in" }, CancellationToken.None);

        var me = await _muni.MeAsync();
        Assert.Equal("Asha Rao", me.FullName);
        Assert.Equal(450, me.Xp);
        Assert.Equal("Pathfinder", me.Rank);
        Assert.Equal(350, me.ToNext);
        Assert.Equal(4, me.Streak);
        Assert.Equal(2, me.QuestsDone);
        Assert.Equal("The Connector", me.Archetype);
        Assert.False(me.Fresh);
    }

    [Fact]
    public void Erased_learner_is_a_tombstone_that_ignores_late_events()
    {
        var learner = _muni.Known(xp: 120);
        learner.CompleteQuest(1, _muni.Clock.GetUtcNow().UtcDateTime);

        learner.Erase(_muni.Clock.GetUtcNow().UtcDateTime);
        learner.ApplyTotals(500, 2, "Pathfinder", 800, 3, _muni.Clock.GetUtcNow().UtcDateTime);
        learner.Register("Yash", "Pohare", _muni.Clock.GetUtcNow().UtcDateTime, _muni.Clock.GetUtcNow().UtcDateTime);
        learner.CompleteQuest(2, _muni.Clock.GetUtcNow().UtcDateTime);
        learner.SetArchetype("The Connector", _muni.Clock.GetUtcNow().UtcDateTime);

        Assert.True(learner.IsErased);
        Assert.Equal((0, "", "", 0, 0, (string?)null), (learner.TotalXp, learner.FirstName, learner.LastName, learner.QuestsDone, learner.StreakDays, learner.Archetype));
    }

    [Fact]
    public async Task Erased_learner_gets_defaults_and_nothing_is_stored()
    {
        _muni.Known(xp: 120).Erase(_muni.Clock.GetUtcNow().UtcDateTime);

        var me = await _muni.MeAsync();
        var reply = await _muni.Service.AskAsync(_muni.Who, new AskRequest("hi", null), CancellationToken.None);

        Assert.True(me.Erased);
        Assert.Equal(0, me.Xp);
        Assert.Equal("Yash", me.First);
        Assert.NotEmpty(reply.Blocks);
        Assert.Empty(await _muni.Service.HistoryAsync(_muni.Who, CancellationToken.None));
    }
}
