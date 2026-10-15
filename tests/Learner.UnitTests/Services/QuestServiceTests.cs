using System.Text.Json;
using Learner.Application.Events;
using Learner.UnitTests.Fakes;

namespace Learner.UnitTests.Services;

public sealed class QuestServiceTests
{
    private readonly LearnerHarness _h = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    [Fact]
    public async Task Saving_parts_merges_answers_shallowly()
    {
        var who = LearnerHarness.NewLearner();

        await _h.Quests.SavePartAsync(who, "hit-me-up", 1, LearnerHarness.Part(new { phone = "9876540697", whatsapp = true }), _ct);
        await _h.Quests.SavePartAsync(who, "hit-me-up", 3, LearnerHarness.Part(new { postal = "411038", whatsapp = false }), _ct);
        var answers = (await _h.Quests.GetAnswersAsync(who, "hit-me-up", _ct)).Value;

        Assert.Equal("9876540697", answers.GetProperty("phone").GetString());
        Assert.Equal("411038", answers.GetProperty("postal").GetString());
        Assert.Equal(JsonValueKind.False, answers.GetProperty("whatsapp").ValueKind);
    }

    [Fact]
    public async Task Unsaved_quest_returns_an_empty_object()
    {
        var answers = (await _h.Quests.GetAnswersAsync(LearnerHarness.NewLearner(), "work-lore", _ct)).Value;
        Assert.Equal(JsonValueKind.Object, answers.ValueKind);
        Assert.Empty(answers.EnumerateObject());
    }

    [Fact]
    public async Task A_step_earns_xp_once_and_only_in_the_quest_in_progress()
    {
        var who = LearnerHarness.NewLearner();

        await _h.Quests.SavePartAsync(who, "who-dis", 1, LearnerHarness.Part(new { firstName = "Asha" }), _ct);
        await _h.Quests.SavePartAsync(who, "who-dis", 1, LearnerHarness.Part(new { firstName = "Asha" }), _ct);
        await _h.Quests.SavePartAsync(who, "hit-me-up", 1, LearnerHarness.Part(new { phone = "1" }), _ct);

        var step = Assert.Single(_h.Store.EventsOf<QuestStepCompleted>());
        Assert.Equal(("who-dis", 1, 1), (step.Slug, step.Quest, step.Part));
        Assert.Equal(20, (await _h.ProgressAsync(who)).Xp);
    }

    [Fact]
    public async Task A_skipped_optional_step_moves_progress_but_never_pays()
    {
        var who = LearnerHarness.NewLearner();
        await _h.FinishQuestsAsync(who, 1);
        _h.Store.Events.Clear();

        await _h.Quests.SavePartAsync(who, "hit-me-up", 2, LearnerHarness.Part(new { backupSkipped = true }, xp: false), _ct);
        await _h.Quests.SavePartAsync(who, "hit-me-up", 2, LearnerHarness.Part(new { backupPhone = "9876500000", backupSkipped = false }), _ct);

        var step = Assert.Single(_h.Store.EventsOf<QuestStepCompleted>());
        Assert.Equal((2, false), (step.Part, step.Paid));
        Assert.Equal(2, _h.Learner(who).PartsDone);
    }

    [Fact]
    public async Task An_optional_step_pays_only_when_something_was_added()
    {
        var who = LearnerHarness.NewLearner();
        await _h.FinishQuestsAsync(who, 3);
        _h.Store.Events.Clear();

        // BR-101: one certificate on the trophy shelf pays; an empty Next on the receipts step doesn't.
        await _h.Quests.SavePartAsync(who, "your-flex-era", 1, LearnerHarness.Part(new { skills = new[] { new { name = "Excel", level = 2 } } }), _ct);
        await _h.Quests.SavePartAsync(who, "your-flex-era", 2, LearnerHarness.Part(new { languages = new[] { new { name = "Hindi" } } }), _ct);
        await _h.Quests.SavePartAsync(who, "your-flex-era", 3, LearnerHarness.Part(new { trophies = new[] { new { name = "NSDC data entry" } } }), _ct);
        await _h.Quests.CompleteAsync(who, "your-flex-era", _ct);
        await _h.FinishQuestsAsync(who, 6);
        await _h.Quests.SavePartAsync(who, "drop-the-receipts", 1, LearnerHarness.Part(new { receipts = Array.Empty<string>() }), _ct);

        var paid = _h.Store.EventsOf<QuestStepCompleted>().ToDictionary(s => (s.Slug, s.Part), s => s.Paid);
        Assert.True(paid[("your-flex-era", 3)]);
        Assert.False(paid[("drop-the-receipts", 1)]);
        Assert.True(paid[("work-lore", 3)]);
    }

    [Fact]
    public async Task Required_steps_pay_even_when_the_client_says_no_xp()
    {
        var who = LearnerHarness.NewLearner();
        await _h.Quests.SavePartAsync(who, "who-dis", 2, LearnerHarness.Part(new { }, xp: false), _ct);
        Assert.True(Assert.Single(_h.Store.EventsOf<QuestStepCompleted>()).Paid);
    }

    [Fact]
    public async Task Job_profile_completed_is_published_once_at_100_percent()
    {
        var who = LearnerHarness.NewLearner();
        await _h.FinishQuestsAsync(who, 7);
        Assert.Empty(_h.Store.EventsOf<JobProfileCompleted>());

        await _h.FinishQuestsAsync(who, 8);
        await _h.Quests.SavePartAsync(who, "vibe-check", 1, LearnerHarness.Part(new { discoverable = true }), _ct);

        Assert.Single(_h.Store.EventsOf<JobProfileCompleted>());
        Assert.NotNull(_h.Learner(who).JobProfileCompletedAt);
    }

    [Fact]
    public async Task A_skipped_optional_step_keeps_the_job_profile_below_100()
    {
        var who = LearnerHarness.NewLearner();
        await _h.FinishQuestsAsync(who, 1);
        await _h.Quests.SavePartAsync(who, "hit-me-up", 2, LearnerHarness.Part(new { backupSkipped = true }, xp: false), _ct);
        await _h.FinishQuestsAsync(who, 8);

        Assert.Empty(_h.Store.EventsOf<JobProfileCompleted>());
        Assert.Equal(17, (await _h.ProfileProgress.GetAsync(who.Id, _ct)).Value.Filled);
    }

    [Fact]
    public void The_catalogue_has_18_steps_and_the_brd_optional_steps()
    {
        Assert.Equal(18, _h.Catalog.Quests.Sum(q => q.Parts.Count));
        Assert.Equal(3, _h.Catalog.Find("work-lore")!.Parts.Count);
        var optional = _h.Catalog.Quests.SelectMany(q => q.Parts.Where(p => p.Optional).Select(p => $"{q.Slug}:{p.Key}"));
        Assert.Equal(new[] { "hit-me-up:backup", "big-brain-stuff:timeline", "your-flex-era:trophies", "drop-the-receipts:receipts" }, optional);
        Assert.Equal(
            new[] { "Main Character Badge", "Link Up Badge", "Big Brain Badge", "Player Card", "Storyteller Badge", "Arc Badge", "Receipts Badge", "Vibe Check Badge" },
            _h.Catalog.Quests.Select(q => q.Badge));
    }

    [Fact]
    public async Task Saving_without_done_keeps_progress()
    {
        var who = LearnerHarness.NewLearner();
        await _h.Quests.SavePartAsync(who, "who-dis", 1, LearnerHarness.Part(new { firstName = "A" }, done: false), _ct);
        Assert.Equal(0, _h.Learner(who).PartsDone);
    }

    [Fact]
    public async Task Quest_1_name_renames_the_learner()
    {
        var who = LearnerHarness.NewLearner("Ash");
        await _h.Quests.SavePartAsync(who, "who-dis", 1, LearnerHarness.Part(new { firstName = "Asha", lastName = "Rao" }), _ct);
        Assert.Equal("Asha Rao", _h.Learner(who).FullName);
    }

    [Fact]
    public async Task Completing_earns_quest_xp_once()
    {
        var who = LearnerHarness.NewLearner();

        var first = (await _h.Quests.CompleteAsync(who, "who-dis", _ct)).Value;
        var again = (await _h.Quests.CompleteAsync(who, "who-dis", _ct)).Value;

        Assert.Equal(50, first.XpEarned);
        Assert.Equal(1, first.Progress.Completed);
        Assert.Equal(1, first.Progress.Badges);
        Assert.Equal(0, again.XpEarned);
        var completed = Assert.Single(_h.Store.EventsOf<QuestCompleted>());
        Assert.Equal("Main Character Badge", completed.Badge);
    }

    [Fact]
    public async Task A_quest_after_the_next_one_is_locked()
    {
        var result = await _h.Quests.CompleteAsync(LearnerHarness.NewLearner(), "big-brain-stuff", _ct);
        Assert.Equal("quest-locked", result.Error!.Code);
    }

    [Theory]
    [InlineData("nope", 1, "quest-not-found")]
    [InlineData("who-dis", 0, "part-not-found")]
    [InlineData("who-dis", 4, "part-not-found")]
    public async Task Unknown_quest_or_part_is_404(string slug, int part, string code)
    {
        var result = await _h.Quests.SavePartAsync(LearnerHarness.NewLearner(), slug, part, LearnerHarness.Part(new { }), _ct);
        Assert.Equal(code, result.Error!.Code);
    }

    [Fact]
    public async Task Oversized_answers_are_refused()
    {
        _h.Settings.MaxQuestAnswersBytes = 100;
        var result = await _h.Quests.SavePartAsync(LearnerHarness.NewLearner(), "work-lore", 1, LearnerHarness.Part(new { text = new string('x', 200) }), _ct);
        Assert.Equal("answers-too-large", result.Error!.Code);
    }

    [Theory]
    [InlineData(0, 1, "Rookie", 400)]
    [InlineData(399, 1, "Rookie", 400)]
    [InlineData(400, 2, "Pathfinder", 800)]
    [InlineData(1250, 4, "Legend", 1600)]
    [InlineData(1700, 5, "Icon", 2000)]
    public void One_level_ladder(int xp, int level, string rank, int target)
    {
        var info = _h.Progress.LevelFor(xp);
        Assert.Equal((level, rank, target), (info.Level, info.Rank, info.Target));
    }
}
