using Learning.Application;
using Learning.Application.Contracts;
using Learning.Application.Handlers;
using Learning.Application.Services;
using Learning.Domain;

namespace Learning.UnitTests;

public sealed class HomeAndStatsTests
{
    private readonly Harness _h = new();

    [Fact]
    public async Task A_new_learner_gets_three_first_moves_and_no_resume_card()
    {
        var feed = await _h.Home.FeedAsync(_h.LearnerId, default);

        Assert.Equal("morning", feed.Daypart);
        Assert.Null(feed.Resume);
        Assert.Equal(new[] { "assessment", "learn", "jobs" }, feed.FirstSteps!.Select(s => s.Id));
        Assert.All(feed.FirstSteps!, s => Assert.False(s.Done));
        Assert.Equal(100, feed.FirstSteps![0].Xp);
        Assert.Null(feed.FirstSteps![1].Xp);
        Assert.Equal("/learn/brain-vita", feed.FirstSteps![1].Link);
        Assert.Equal(new FirstMovesBonusDto(50, false), feed.FirstMovesBonus);
        Assert.Equal(3, feed.Pathways.Count);
        Assert.Equal(new[] { "skill", "industry", "blue-collar", "practice", "course" }, feed.Knooks.Select(k => k.Slug));
        Assert.Equal(0, feed.RewardsReady);
        Assert.Equal(0, feed.UnreadNotifications);
    }

    [Fact]
    public void Daypart_follows_the_market_time_zone()
    {
        _h.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero)); // 15:30 IST
        Assert.Equal("afternoon", _h.Home.Daypart());
        _h.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 15, 13, 0, 0, TimeSpan.Zero)); // 18:30 IST
        Assert.Equal("evening", _h.Home.Daypart());
    }

    [Fact]
    public async Task Claiming_before_all_moves_are_done_is_409_then_pays_once()
    {
        Assert.Equal("first-moves-not-done", (await _h.Home.ClaimFirstMovesAsync(_h.LearnerId, default)).Error!.Code);

        await new AssessmentSubmittedHandler(_h.Store, _h.Clock).HandleAsync(new AssessmentSubmitted(_h.LearnerId) { TenantId = "skillmuni-in" }, default);
        Assert.True((await _h.Home.CompleteStepAsync(_h.LearnerId, "jobs", default)).IsSuccess);
        Assert.Equal("first-moves-not-done", (await _h.Home.ClaimFirstMovesAsync(_h.LearnerId, default)).Error!.Code);

        await _h.PassNextAsync("brain-vita", "verbal-reasoning");
        var feed = await _h.Home.FeedAsync(_h.LearnerId, default);
        Assert.All(feed.FirstSteps!, s => Assert.True(s.Done));

        Assert.Equal(50, (await _h.Home.ClaimFirstMovesAsync(_h.LearnerId, default)).Value.Xp);
        Assert.Equal(0, (await _h.Home.ClaimFirstMovesAsync(_h.LearnerId, default)).Value.Xp);
        Assert.Single(_h.Store.Published<FirstMovesClaimed>());

        var after = await _h.Home.FeedAsync(_h.LearnerId, default);
        Assert.Null(after.FirstSteps);
        Assert.Null(after.FirstMovesBonus);
    }

    [Fact]
    public async Task Unknown_first_step_is_404_and_visit_steps_for_others_are_no_ops()
    {
        Assert.Equal("step-not-found", (await _h.Home.CompleteStepAsync(_h.LearnerId, "nope", default)).Error!.Code);
        Assert.True((await _h.Home.CompleteStepAsync(_h.LearnerId, "assessment", default)).IsSuccess);
        Assert.Empty(_h.Store.Lobby);
    }

    [Fact]
    public async Task Resume_card_points_at_the_next_brief_of_the_last_topic()
    {
        for (var i = 0; i < 10; i++)
        {
            await _h.PassNextAsync("brain-vita", "non-verbal-reasoning");
        }

        var card = (await _h.Home.FeedAsync(_h.LearnerId, default)).Resume!;
        Assert.Equal(("Brain Vita", "Non-Verbal Reasoning"), (card.Module, card.Topic));
        Assert.Equal((11, 11), (card.Brief, card.Briefs));
        Assert.Equal("1 quiz away from 100% mastery", card.Note);
        Assert.Equal(90, card.Percent);
        Assert.Equal(3, card.Xp);
        Assert.Equal("assets/home/tile-brain-vita.png", card.Art);
        Assert.Equal("/learn/brain-vita/non-verbal-reasoning", card.Link);

        // Topic finished: the card moves on to the module's next unfinished topic.
        await _h.PassNextAsync("brain-vita", "non-verbal-reasoning");
        var next = (await _h.Home.FeedAsync(_h.LearnerId, default)).Resume!;
        Assert.Equal("Verbal Reasoning", next.Topic);
        Assert.Equal("51 quizzes away from 100% mastery", next.Note);
    }

    [Fact]
    public async Task Stats_list_every_module_closest_to_done_first()
    {
        var empty = await _h.Stats.LearningAsync(_h.LearnerId, default);
        Assert.All(empty.Courses, c => Assert.Equal("not-started", c.Status));
        Assert.Equal(Harness.Content.Modules.Count, empty.Courses.Count);
        Assert.Null(empty.CurrentId);
        Assert.Null(empty.Nudge);
        Assert.Equal("brain-vita", empty.Courses[0].Id);
        Assert.Equal(51 + (11 * 3) + 37 + 6, empty.Courses[0].XpOnFinish); // non-verbal briefs use the 3-question quiz

        for (var i = 0; i < 5; i++)
        {
            await _h.PassNextAsync("speak-up", i < 3 ? "getting-started" : "going-deeper");
        }

        await _h.PassNextAsync("brain-vita", "data-interpretation");
        var stats = await _h.Stats.LearningAsync(_h.LearnerId, default);
        Assert.Equal("speak-up", stats.Courses[0].Id);
        Assert.Equal(83.33, stats.Courses[0].Percent);
        Assert.Equal("in-progress", stats.Courses[0].Status);
        Assert.Equal(1, stats.Courses[0].XpOnFinish);
        Assert.Equal("/learn/speak-up", stats.Courses[0].Link);
        Assert.Equal("brain-vita", stats.Courses[1].Id);
        Assert.Equal("brain-vita", stats.CurrentId);
        Assert.Null(stats.Nudge);

        await _h.PassNextAsync("speak-up", "going-deeper");
        var done = await _h.Stats.LearningAsync(_h.LearnerId, default);
        Assert.Equal("done", done.Courses[^1].Status);
        Assert.Equal(100, done.Courses[^1].Percent);
    }

    [Fact]
    public void Percent_rounds_down_to_two_decimals()
    {
        Assert.Equal(98.86, LearningStatsService.Percent(87, 88));
        Assert.Equal(0, LearningStatsService.Percent(0, 0));
        Assert.Equal(100, LearningStatsService.Percent(5, 5));
    }

    [Fact]
    public void Validators_check_shapes()
    {
        Assert.False(new ActivityRequestValidator().Validate(new ActivityRequest(0)).IsValid);
        Assert.False(new ActivityRequestValidator().Validate(new ActivityRequest(61)).IsValid);
        Assert.False(new ActivityRequestValidator().Validate(new ActivityRequest(null)).IsValid);
        Assert.True(new ActivityRequestValidator().Validate(new ActivityRequest(60)).IsValid);
        Assert.False(new BookmarkRequestValidator().Validate(new BookmarkRequest(null)).IsValid);
        Assert.False(new QuizAnswersRequestValidator().Validate(new QuizAnswersRequest(null)).IsValid);
        var tooMany = Enumerable.Range(0, 51).ToDictionary(i => $"q{i}", i => "o");
        Assert.False(new QuizAnswersRequestValidator().Validate(new QuizAnswersRequest(tooMany)).IsValid);
        Assert.True(new QuizAnswersRequestValidator().Validate(new QuizAnswersRequest(new() { ["nv-q1"] = "nv-q1-o1" })).IsValid);
    }

    [Fact]
    public void Every_lobby_tile_opens_a_module_with_briefs_and_quizzes()
    {
        var content = Harness.Content;
        foreach (var tile in content.Knooks.SelectMany(k => k.Modules))
        {
            var module = content.Module(tile.Slug);
            Assert.NotNull(module);
            Assert.Equal($"/learn/{tile.Slug}", tile.Link);
            Assert.All(module!.Topics, t =>
            {
                Assert.NotEmpty(t.Briefs);
                Assert.All(Enumerable.Range(0, t.Briefs.Count), i => Assert.NotEmpty(t.QuestionsFor(i)));
            });
        }

        Assert.Equal(content.Modules.Count, content.Modules.Select(m => m.Slug).Distinct().Count());
        Assert.Equal("Digital Foundations Knook", content.Module("digital-basics")!.Knook.Title);
        Assert.All(content.Modules.SelectMany(m => m.Topics).SelectMany(t => t.Questions), q => Assert.InRange(q.Answer, 0, q.Options.Count - 1));
    }

    [Fact]
    public void Brief_keys_parse_and_reject_garbage()
    {
        Assert.True(BriefKey.TryParse("brain-vita~non-verbal-reasoning~10", out var key));
        Assert.Equal(("brain-vita", "non-verbal-reasoning", 10), (key.Module, key.Topic, key.Index));
        Assert.False(BriefKey.TryParse("a~b", out _));
        Assert.False(BriefKey.TryParse("a~b~-1", out _));
        Assert.False(BriefKey.TryParse(null, out _));
    }
}
