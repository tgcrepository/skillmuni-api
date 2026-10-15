using Assessment.Application;
using Assessment.Application.Handlers;
using Assessment.Application.Services;
using Assessment.Domain;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Assessment.UnitTests;

public sealed class CareerAssessmentServiceTests
{
    private readonly AssessmentHarness _h = new();

    [Fact]
    public async Task A_new_learner_sees_the_intro_state_without_a_row_being_written()
    {
        var state = await _h.Service.StateAsync(_h.LearnerId, CancellationToken.None);

        Assert.Equal(("new", 36, 4, 0, 0, true, true, 0), (state.Status, state.Pairs.Count, state.Islands.Count, state.Current, state.Xp, state.EarnsXp, state.Remind, state.Attempts));
        Assert.All(state.Answers, a => Assert.True(a.IsNone));
        Assert.Empty(_h.Store.Journeys);
    }

    [Fact]
    public async Task Clearing_an_island_pays_25_once_and_publishes_the_event()
    {
        var saved = await _h.SaveAsync(AssessmentHarness.Answered(8), 8);
        Assert.Equal((0, 0), (saved.Xp, saved.XpEarned));

        saved = await _h.SaveAsync(AssessmentHarness.Answered(9), 9);
        Assert.Equal((25, 25), (saved.Xp, saved.XpEarned));
        var cleared = Assert.Single(_h.EventsOf<IslandCleared>());
        Assert.Equal((_h.LearnerId, 1, 1, true, "skillmuni-in"), (cleared.LearnerId, cleared.Attempt, cleared.Island, cleared.FirstAttempt, cleared.TenantId));

        // A replayed save (BR-133) and later picks in the same island earn nothing more.
        saved = await _h.SaveAsync(AssessmentHarness.Answered(9), 9);
        Assert.Equal((25, 0), (saved.Xp, saved.XpEarned));
        Assert.Single(_h.EventsOf<IslandCleared>());

        var state = await _h.Service.StateAsync(_h.LearnerId, CancellationToken.None);
        Assert.Equal(("in-progress", 9, 25), (state.Status, state.Current, state.Xp));
    }

    [Fact]
    public async Task Skipped_pairs_dont_clear_an_island_and_the_last_island_waits_for_submit()
    {
        var answers = AssessmentHarness.All();
        answers[4] = Answer.Skip;
        var saved = await _h.SaveAsync(answers, 35);

        Assert.Equal((50, 50), (saved.Xp, saved.XpEarned));
        Assert.Equal(new[] { 2, 3 }, _h.EventsOf<IslandCleared>().Select(e => e.Island));
    }

    [Fact]
    public async Task Submit_refuses_unanswered_or_skipped_pairs()
    {
        var skipped = AssessmentHarness.All();
        skipped[20] = Answer.Skip;

        Assert.Equal("incomplete", (await _h.SubmitAsync(skipped)).Error?.Code);
        Assert.Equal("incomplete", (await _h.SubmitAsync(AssessmentHarness.Answered(35))).Error?.Code);
        Assert.Equal("incomplete", (await _h.SubmitAsync([.. AssessmentHarness.All(), Answer.FromNotch(1)])).Error?.Code);
        Assert.Empty(_h.Store.Attempts);
    }

    [Fact]
    public async Task First_submit_pays_100_in_all_and_publishes_the_result()
    {
        foreach (var n in new[] { 9, 18, 27, 36 })
        {
            await _h.SaveAsync(AssessmentHarness.Answered(n, notch: 0), n);
        }

        var result = (await _h.SubmitAsync(AssessmentHarness.All(notch: 0))).Value;

        Assert.Equal((1, "2026-10-16", 100, 2), (result.Attempt, result.Date, result.XpEarned, result.MandatoryCourses));
        Assert.Equal(108, result.Scores.Values.Sum());
        Assert.Equal(Drivers.All, result.Scores.Keys);
        Assert.Null(result.Previous);
        Assert.Equal(9, result.Drivers.Count);
        Assert.Equal(result.Archetype.Name, Assert.Single(result.Attempts).Archetype);

        var submitted = Assert.Single(_h.EventsOf<AttemptSubmitted>());
        Assert.Equal((1, true, result.Archetype.Id, result.Archetype.Summary), (submitted.Attempt, submitted.FirstAttempt, submitted.ArchetypeId, submitted.ArchetypeSummary));
        Assert.Equal(result.Ranking.Take(3), submitted.TopDriverIds);
        Assert.Equal(submitted.TopDriverIds.Select(d => AssessmentHarness.Content.Content.FindDriver(d)!.Name), submitted.TopDrivers);
        Assert.Equal(3, _h.EventsOf<IslandCleared>().Count);

        var state = await _h.Service.StateAsync(_h.LearnerId, CancellationToken.None);
        Assert.Equal(("done", 0, 0, false, 1), (state.Status, state.Current, state.Xp, state.EarnsXp, state.Attempts));
        Assert.All(state.Answers, a => Assert.True(a.IsNone));
    }

    [Fact]
    public async Task Submitting_straight_away_still_clears_islands_1_to_3()
    {
        var result = (await _h.SubmitAsync(AssessmentHarness.All())).Value;

        Assert.Equal(100, result.XpEarned);
        Assert.Equal(new[] { 1, 2, 3 }, _h.EventsOf<IslandCleared>().Select(e => e.Island));
        Assert.All(_h.EventsOf<IslandCleared>(), e => Assert.Equal((1, true), (e.Attempt, e.FirstAttempt)));
    }

    [Fact]
    public async Task Retakes_earn_nothing_and_compare_with_the_previous_attempt()
    {
        var first = (await _h.SubmitAsync(AssessmentHarness.All(notch: 0))).Value;
        _h.Clock.Advance(TimeSpan.FromDays(3));

        var state = await _h.Service.RetakeAsync(_h.LearnerId, CancellationToken.None);
        Assert.Equal(("done", false, 0), (state.Status, state.EarnsXp, state.Xp));

        var saved = await _h.SaveAsync(AssessmentHarness.Answered(9, notch: 3), 9);
        Assert.Equal((0, 0), (saved.Xp, saved.XpEarned));
        var cleared = _h.EventsOf<IslandCleared>().Last();
        Assert.Equal((2, 1, false), (cleared.Attempt, cleared.Island, cleared.FirstAttempt));

        var second = (await _h.SubmitAsync(AssessmentHarness.All(notch: 3))).Value;
        Assert.Equal((2, 0, "2026-10-19"), (second.Attempt, second.XpEarned, second.Date));
        Assert.Equal(first.Scores, second.Previous);
        Assert.Equal(new[] { 2, 1 }, second.Attempts.Select(a => a.N));
        Assert.False(_h.EventsOf<AttemptSubmitted>().Last().FirstAttempt);

        Assert.Equal(1, (await _h.Service.ResultAsync(_h.LearnerId, 1, CancellationToken.None)).Value.Attempt);
        Assert.Equal(2, (await _h.Service.ResultAsync(_h.LearnerId, null, CancellationToken.None)).Value.Attempt);
        Assert.Equal(2, (await _h.Service.ResultAsync(_h.LearnerId, 99, CancellationToken.None)).Value.Attempt);
        Assert.Equal(100, (await _h.Service.ResultAsync(_h.LearnerId, 1, CancellationToken.None)).Value.XpEarned);
    }

    [Fact]
    public async Task No_result_before_the_first_submit()
    {
        var result = await _h.Service.ResultAsync(_h.LearnerId, null, CancellationToken.None);
        Assert.Equal(AssessmentErrors.NoResult, result.Error);
    }

    [Fact]
    public async Task Restart_keeps_cleared_islands_so_they_never_pay_twice()
    {
        await _h.SaveAsync(AssessmentHarness.Answered(10), 10);
        var state = await _h.Service.RestartAsync(_h.LearnerId, CancellationToken.None);
        Assert.Equal(("new", 0, 25, true), (state.Status, state.Current, state.Xp, state.EarnsXp));

        var saved = await _h.SaveAsync(AssessmentHarness.Answered(9), 9);
        Assert.Equal((25, 0), (saved.Xp, saved.XpEarned));
        Assert.Single(_h.EventsOf<IslandCleared>());
    }

    [Fact]
    public async Task A_retake_before_any_submit_is_a_restart_and_keeps_the_xp()
    {
        await _h.SaveAsync(AssessmentHarness.Answered(9), 9);
        var state = await _h.Service.RetakeAsync(_h.LearnerId, CancellationToken.None);
        Assert.Equal((true, 25), (state.EarnsXp, state.Xp));
    }

    [Fact]
    public async Task Remind_is_stored()
    {
        Assert.False((await _h.Service.SetRemindAsync(_h.LearnerId, false, CancellationToken.None)).Remind);
        Assert.False((await _h.Service.StateAsync(_h.LearnerId, CancellationToken.None)).Remind);
        Assert.True((await _h.Service.SetRemindAsync(_h.LearnerId, true, CancellationToken.None)).Remind);
    }

    [Fact]
    public async Task Registration_starts_a_journey_and_erasure_removes_everything()
    {
        var registered = new AccountRegisteredHandler(_h.Store, _h.Clock);
        await registered.HandleAsync(new AccountRegistered(_h.LearnerId, "Asha", "K", "IN") { TenantId = "skillmuni-in" }, CancellationToken.None);
        await registered.HandleAsync(new AccountRegistered(_h.LearnerId, "Asha", "K", "IN") { TenantId = "skillmuni-in" }, CancellationToken.None);
        Assert.Single(_h.Store.Journeys);

        await _h.SubmitAsync(AssessmentHarness.All());
        var tombstones = new SkillMuni.Testing.FakeErasureTombstones();
        await new AccountErasedHandler(_h.Store, tombstones, NullLogger<AccountErasedHandler>.Instance)
            .HandleAsync(new AccountErased(_h.LearnerId) { TenantId = "skillmuni-in" }, CancellationToken.None);

        Assert.Empty(_h.Store.Journeys);
        Assert.Empty(_h.Store.Attempts);
        Assert.Contains(_h.LearnerId, tombstones.Erased);
    }

    [Fact]
    public void Skill_stats_show_the_empty_state_with_munis_pick()
    {
        var dashboard = new AssessmentStatsService(MsOptions.Create(new AssessmentOptions())).Dashboard();
        Assert.Empty(dashboard.Results);
        Assert.Equal((20, 20, "Digital Skills", "/assessments/digital-skills"), (dashboard.Available, dashboard.FirstCardXp, dashboard.Recommended.Title, dashboard.Recommended.Url));
    }
}

public sealed class ValidatorTests
{
    private readonly SaveAnswersRequestValidator _save = new(AssessmentHarness.Content);

    [Fact]
    public void Save_needs_one_answer_per_pair_and_a_current_pair()
    {
        Assert.True(_save.Validate(new SaveAnswersRequest(AssessmentHarness.Answered(3), 3)).IsValid);
        Assert.True(_save.Validate(new SaveAnswersRequest(AssessmentHarness.All(), 36)).IsValid);
        Assert.False(_save.Validate(new SaveAnswersRequest(AssessmentHarness.Answered(3).Take(10).ToList(), 3)).IsValid);
        Assert.False(_save.Validate(new SaveAnswersRequest(null, 3)).IsValid);
        Assert.False(_save.Validate(new SaveAnswersRequest(AssessmentHarness.All(), 37)).IsValid);
        Assert.False(_save.Validate(new SaveAnswersRequest(AssessmentHarness.All(), null)).IsValid);
    }

    [Fact]
    public void Remind_needs_a_value()
    {
        IValidator<RemindRequest> validator = new RemindRequestValidator();
        Assert.True(validator.Validate(new RemindRequest(false)).IsValid);
        Assert.False(validator.Validate(new RemindRequest(null)).IsValid);
    }
}
