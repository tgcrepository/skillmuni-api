using System.Text.Json;
using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Application.Events;
using Learner.Application.Handlers;
using Learner.Domain;
using Learner.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Learner.UnitTests.Services;

public sealed class SupportingServiceTests
{
    private readonly LearnerHarness _h = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    [Fact]
    public async Task Drafts_round_trip_any_json_value()
    {
        var id = Guid.NewGuid();
        await _h.Drafts.SaveAsync(id, "quest:work-lore:2", JsonSerializer.SerializeToElement(new[] { 1, 2, 3 }), _ct);
        await _h.Drafts.SaveAsync(id, "quest:work-lore:2", JsonSerializer.SerializeToElement("latest"), _ct);

        Assert.Equal("latest", (await _h.Drafts.GetAsync(id, "quest:work-lore:2", _ct)).Value.GetString());
        Assert.Single(_h.Store.Drafts);
        Assert.Equal("draft-not-found", (await _h.Drafts.GetAsync(id, "other", _ct)).Error!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("../etc")]
    public async Task Draft_keys_are_restricted(string key)
    {
        var result = await _h.Drafts.SaveAsync(Guid.NewGuid(), key, JsonSerializer.SerializeToElement(1), _ct);
        Assert.Equal("invalid-key", result.Error!.Code);
    }

    [Fact]
    public async Task Oversized_drafts_are_refused()
    {
        _h.Settings.MaxDraftBytes = 10;
        var result = await _h.Drafts.SaveAsync(Guid.NewGuid(), "k", JsonSerializer.SerializeToElement(new string('x', 20)), _ct);
        Assert.Equal("draft-too-large", result.Error!.Code);
    }

    [Fact]
    public async Task Skill_search_clamps_the_limit_and_falls_back_to_english()
    {
        await _h.Skills.SearchAsync("  excel ", "fr-FR", 500, _ct);
        Assert.Equal(("excel", "en", 50), _h.Esco.LastCall);

        await _h.Skills.SearchAsync("excel", "hi-IN", null, _ct);
        Assert.Equal(("excel", "hi", 24), _h.Esco.LastCall);
    }

    [Fact]
    public async Task Empty_skill_search_returns_nothing_without_calling_esco()
    {
        var result = await _h.Skills.SearchAsync(" ", "en", 10, _ct);
        Assert.Equal(0, result.Value.Total);
        Assert.Null(_h.Esco.LastCall);
    }

    [Fact]
    public async Task Esco_down_is_503()
    {
        _h.Esco.Next = Lookup<EscoSearchDto>.Unavailable();
        Assert.Equal("esco-unavailable", (await _h.Skills.SearchAsync("excel", "en", 10, _ct)).Error!.Code);
    }

    [Fact]
    public async Task Profile_progress_counts_reached_steps_and_skipped_optional_ones()
    {
        var who = LearnerHarness.NewLearner();
        await _h.FinishQuestsAsync(who, 2);
        await _h.Quests.SavePartAsync(who, "hit-me-up", 2, LearnerHarness.Part(new { backupSkipped = true }), _ct);
        await _h.Quests.SavePartAsync(who, "big-brain-stuff", 1, LearnerHarness.Part(new { level = "graduate" }), _ct);

        var progress = (await _h.ProfileProgress.GetAsync(who.Id, _ct)).Value;

        Assert.Equal(18, progress.Total);
        Assert.Equal(6, progress.Filled);
        Assert.Equal(33, progress.Percent);
        var q2 = progress.Quests.Single(q => q.Slug == "hit-me-up");
        Assert.Equal("backup", Assert.Single(q2.Left).Key);
        var q3 = progress.Quests.Single(q => q.Slug == "big-brain-stuff");
        Assert.Equal(new[] { 2, 3 }, q3.Left.Select(l => l.Part));
    }

    [Fact]
    public async Task Preferences_round_trip()
    {
        var who = LearnerHarness.NewLearner();
        await _h.Preferences.SaveAsync(who, new PreferencesRequest(false, true, "hi-IN"), _ct);
        Assert.Equal(new PreferencesDto(false, true, "hi-IN"), await _h.Preferences.GetAsync(who, _ct));
    }

    [Fact]
    public async Task Registration_event_creates_the_learner_and_seeds_quest_1()
    {
        var handler = new AccountRegisteredHandler(_h.Store, _h.Store, _h.Clock);
        var id = Guid.NewGuid();

        await handler.HandleAsync(new AccountRegistered(id, "Asha", "Rao", "asha@example.com", null, "IN", "email") { TenantId = LearnerHarness.Tenant }, _ct);

        var learner = Assert.Single(_h.Store.Learners);
        Assert.Equal(("Asha Rao", "asha@example.com"), (learner.FullName, learner.Email));
        var answers = JsonDocument.Parse(Assert.Single(_h.Store.Answers).AnswersJson).RootElement;
        Assert.Equal("Rao", answers.GetProperty("lastName").GetString());
    }

    [Fact]
    public async Task Registration_event_completes_a_learner_a_fast_request_created()
    {
        var who = LearnerHarness.NewLearner();
        await _h.ProgressAsync(who);
        var handler = new AccountRegisteredHandler(_h.Store, _h.Store, _h.Clock);

        await handler.HandleAsync(new AccountRegistered(who.Id, "Asha", "Rao", null, "+919876540697", "IN", "phone") { TenantId = LearnerHarness.Tenant }, _ct);

        var learner = Assert.Single(_h.Store.Learners);
        Assert.Equal(("Asha Rao", "+919876540697"), (learner.FullName, learner.Phone));
    }

    [Fact]
    public async Task Erasure_deletes_files_then_rows()
    {
        var who = LearnerHarness.NewLearner();
        await _h.Profile.UploadPhotoAsync(who, new UploadedFile("p.png", "image/png", 8, new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])), _ct);
        await _h.Quests.SavePartAsync(who, "who-dis", 1, LearnerHarness.Part(new { firstName = "Asha" }), _ct);
        var handler = new AccountErasedHandler(_h.Store, _h.Blobs, NullLogger<AccountErasedHandler>.Instance);

        await handler.HandleAsync(new AccountErased(who.Id) { TenantId = LearnerHarness.Tenant }, _ct);

        Assert.Empty(_h.Store.Learners);
        Assert.Empty(_h.Store.Answers);
        Assert.Empty(_h.Blobs.Blobs);
    }

    [Fact]
    public async Task Claim_confirmation_counts_quests_1_to_5_as_done_and_publishes_their_step_and_quest_events()
    {
        var who = LearnerHarness.NewLearner();
        await _h.FinishQuestsAsync(who, 1);
        _h.Store.Events.Clear();
        var handler = new ClaimConfirmedHandler(_h.Store, _h.Catalog, _h.JobProfileCompletion, _h.Store, _h.Clock, NullLogger<ClaimConfirmedHandler>.Instance);

        await handler.HandleAsync(new ClaimConfirmed(Guid.NewGuid(), who.Id) { TenantId = LearnerHarness.Tenant }, _ct);
        await handler.HandleAsync(new ClaimConfirmed(Guid.NewGuid(), who.Id) { TenantId = LearnerHarness.Tenant }, _ct);

        Assert.Equal(5, (await _h.ProgressAsync(who)).Completed);

        // BR-119: quests 2–5 (quest 1 was already done) through the normal path: 12 paid steps and 4 quests.
        var steps = _h.Store.EventsOf<QuestStepCompleted>().ToList();
        Assert.Equal(12, steps.Count);
        Assert.All(steps, s => Assert.True(s.Paid));
        Assert.Equal(new[] { 2, 3, 4, 5 }, _h.Store.EventsOf<QuestCompleted>().Select(q => q.Quest));
        Assert.Equal("Storyteller Badge", _h.Store.EventsOf<QuestCompleted>().Last().Badge);
    }

    [Fact]
    public async Task Claim_for_a_new_learner_pays_550_through_the_events()
    {
        var who = LearnerHarness.NewLearner();
        await _h.ProgressAsync(who);

        await new ClaimConfirmedHandler(_h.Store, _h.Catalog, _h.JobProfileCompletion, _h.Store, _h.Clock, NullLogger<ClaimConfirmedHandler>.Instance)
            .HandleAsync(new ClaimConfirmed(Guid.NewGuid(), who.Id) { TenantId = LearnerHarness.Tenant }, _ct);

        var xp = (_h.Store.EventsOf<QuestStepCompleted>().Count(s => s.Paid) * 20) + (_h.Store.EventsOf<QuestCompleted>().Count() * 50);
        Assert.Equal(550, xp);
    }

    [Fact]
    public async Task Gamification_totals_drive_the_progress_once_the_estimate_window_passes()
    {
        var who = LearnerHarness.NewLearner();
        await _h.Quests.CompleteAsync(who, "who-dis", _ct);
        var handler = new XpAwardedHandler(_h.Store, _h.Clock);

        await handler.HandleAsync(new XpAwarded(who.Id, 450, 2, "Pathfinder", 400, 800, 1, 1) { TenantId = LearnerHarness.Tenant }, _ct);
        _h.Clock.Advance(TimeSpan.FromMinutes(10));

        var progress = await _h.ProgressAsync(who);
        Assert.Equal((450, 2, "Pathfinder", 800), (progress.Xp, progress.Level, progress.Rank, progress.Target));
    }

    [Fact]
    public void Quest_catalogue_has_eight_quests_in_order()
    {
        Assert.Equal(Enumerable.Range(1, 8), _h.Catalog.Quests.Select(q => q.Number));
        Assert.Equal("vibe-check", _h.Catalog.Quests[^1].Slug);
        Assert.IsType<Quest>(_h.Catalog.Find("work-lore"));
    }

    [Fact]
    public async Task Cv_prefill_fills_gaps_but_keeps_what_the_learner_typed()
    {
        var who = LearnerHarness.NewLearner();
        await _h.Quests.SavePartAsync(who, "hit-me-up", 1, LearnerHarness.Part(new { phone = "9123456789" }), _ct);
        var prefill = new Dictionary<string, JsonElement>
        {
            ["hit-me-up"] = JsonSerializer.SerializeToElement(new { phone = "9876540697", email = "asha@example.com" }),
            ["work-lore"] = JsonSerializer.SerializeToElement(new { workTypes = new[] { "internship" } }),
            ["not-a-quest"] = JsonSerializer.SerializeToElement(new { x = 1 }),
        };

        await new CvParsedHandler(_h.Store, _h.Store, _h.Catalog, _h.Clock)
            .HandleAsync(new CvParsed(Guid.NewGuid(), who.Id, null, prefill) { TenantId = LearnerHarness.Tenant }, _ct);

        var contact = (await _h.Quests.GetAnswersAsync(who, "hit-me-up", _ct)).Value;
        Assert.Equal("9123456789", contact.GetProperty("phone").GetString());
        Assert.Equal("asha@example.com", contact.GetProperty("email").GetString());
        Assert.Equal("internship", (await _h.Quests.GetAnswersAsync(who, "work-lore", _ct)).Value.GetProperty("workTypes")[0].GetString());
        Assert.DoesNotContain(_h.Store.Answers, a => a.Slug == "not-a-quest");

        // Kept for the Job Profile's "Upload a newer CV" suggestions.
        Assert.Contains("asha@example.com", Assert.Single(_h.Store.JobProfiles).CvPrefillJson, StringComparison.Ordinal);
    }
}
