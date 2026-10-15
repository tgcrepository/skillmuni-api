using Gamification.Application;
using Gamification.Application.Handlers;
using Gamification.Application.Services;
using SkillMuni.Common.Paging;

namespace Gamification.UnitTests;

public sealed class EngineTests
{
    private const string Tenant = "skillmuni-in";
    private readonly GameHarness _h = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private Task StepAsync(Guid id, string slug, int part, bool paid = true) =>
        new QuestStepHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuestStepCompleted(id, slug, 1, part, paid) { TenantId = Tenant }, _ct);

    private Task CompleteAsync(Guid id, string slug, int quest = 1) =>
        new QuestCompletedHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuestCompleted(id, slug, quest, "Main Character Badge") { TenantId = Tenant }, _ct);

    private Task QuizAsync(Guid id, string quiz, int correct, int total, bool first = true, DateTime occurredAt = default) =>
        new QuizSubmittedHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(
            new QuizSubmitted(id, quiz, "b1", "verbal", correct, total, first) { TenantId = Tenant, OccurredAt = occurredAt }, _ct);

    private Task MinutesAsync(Guid id, int minutes) =>
        new LearningActivityHandler(_h.Engine(), _h.Clock).HandleAsync(new LearningActivity(id, minutes) { TenantId = Tenant }, _ct);

    private Task BriefAsync(Guid id, string course, bool first = true) =>
        new BriefCompletedHandler(_h.Engine(), _h.Clock).HandleAsync(new BriefCompleted(id, "b1", "verbal", course, first) { TenantId = Tenant }, _ct);

    private Task IslandAsync(Guid id, int island, bool first = true) =>
        new IslandClearedHandler(_h.Engine(), _h.Options).HandleAsync(new IslandCleared(id, 1, island, first) { TenantId = Tenant }, _ct);

    private Task SubmitAsync(Guid id, bool first = true) =>
        new AssessmentSubmittedHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(
            new AssessmentSubmitted(id, first ? 1 : 2, first, "People Pro", "Loves people", ["people"]) { TenantId = Tenant }, _ct);

    private Task FeedbackAsync(Guid id) =>
        new FeedbackSentHandler(_h.Engine(), _h.Store, _h.Clock, _h.Options).HandleAsync(new FeedbackSent(id, 5) { TenantId = Tenant }, _ct);

    [Fact]
    public async Task A_quest_step_pays_once_and_publishes_totals()
    {
        var id = await _h.JoinAsync("Asha Rao");

        await StepAsync(id, "who-dis", 1);
        await StepAsync(id, "who-dis", 1);

        Assert.Single(_h.Store.Ledger);
        Assert.Equal(20, _h.Player(id).TotalXp);
        var totals = _h.Store.Events.OfType<XpAwarded>().Last();
        Assert.Equal((20, 1, "Rookie", 0, 400), (totals.TotalXp, totals.Level, totals.Rank, totals.LevelStartXp, totals.NextLevelXp));
    }

    [Fact]
    public async Task An_unpaid_optional_step_counts_but_pays_nothing()
    {
        var id = await _h.JoinAsync("Asha Rao");

        await StepAsync(id, "hit-me-up", 2, paid: false);

        Assert.Empty(_h.Store.Ledger);
        Assert.Equal(2, _h.Player(id).Counter("quest.hit-me-up.parts"));
    }

    [Fact]
    public async Task Events_before_registration_create_the_player()
    {
        var id = Guid.NewGuid();
        await StepAsync(id, "who-dis", 1);
        Assert.Equal(20, _h.Player(id).TotalXp);
    }

    [Fact]
    public async Task Finishing_a_quest_pays_once_and_earns_only_its_badge()
    {
        var id = await _h.JoinAsync("Asha Rao");

        await CompleteAsync(id, "who-dis");
        await CompleteAsync(id, "who-dis");

        Assert.Equal(50, _h.Player(id).TotalXp);
        var earned = Assert.Single(_h.Store.Events.OfType<BadgeEarned>());
        Assert.Equal(("quest-who-dis", "Main Character Badge"), (earned.BadgeId, earned.BadgeName));

        // §4.3: catalogue badges (Founding Learner…) are not awarded in Phase 1.
        Assert.Equal(1, _h.Player(id).Badges);
    }

    [Fact]
    public async Task Catalogue_badges_are_awarded_only_when_the_flag_is_on()
    {
        var id = await _h.JoinAsync("Asha Rao");
        await QuizAsync(id, "q1", 5, 5);
        Assert.Empty(_h.Store.Awards);

        _h.Settings.Badges.AwardCatalogue = true;
        await QuizAsync(id, "q2", 5, 5);
        Assert.Contains(_h.Store.Awards, a => a.BadgeId == "quiz-starter");
        Assert.Contains(_h.Store.Awards, a => a.BadgeId == "perfect-score");
    }

    [Fact]
    public async Task Streak_counts_first_quiz_submits_on_consecutive_days_and_restarts_after_a_gap()
    {
        var id = await _h.JoinAsync("Asha Rao");
        await QuizAsync(id, "q1", 3, 5);
        _h.Clock.Advance(TimeSpan.FromDays(1));
        await QuizAsync(id, "q2", 3, 5);
        _h.Clock.Advance(TimeSpan.FromDays(1));
        await QuizAsync(id, "q3", 3, 5);

        Assert.Equal(3, _h.Player(id).StreakDays);

        _h.Clock.Advance(TimeSpan.FromDays(3));
        await QuizAsync(id, "q4", 3, 5);
        Assert.Equal((1, 3), (_h.Player(id).StreakDays, _h.Player(id).BestStreak));
    }

    [Fact]
    public async Task Retakes_learning_time_and_other_xp_do_not_keep_the_streak()
    {
        var id = await _h.JoinAsync("Asha Rao");
        await MinutesAsync(id, 20);
        await BriefAsync(id, "brain-vita");
        await StepAsync(id, "who-dis", 1);
        await QuizAsync(id, "q1", 5, 5, first: false);

        Assert.Equal(0, _h.Player(id).StreakDays);
        Assert.Empty(_h.Store.Days);
    }

    [Fact]
    public async Task Streak_day_is_the_learners_local_date_of_the_submit()
    {
        var id = await _h.JoinAsync("Asha Rao");

        // 13 Oct 20:00 UTC is 14 Oct 01:30 in Kolkata.
        await QuizAsync(id, "q1", 3, 5, occurredAt: new DateTime(2026, 10, 13, 20, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateOnly(2026, 10, 14), Assert.Single(_h.Store.Days).Day);
    }

    [Fact]
    public async Task Streaks_earn_no_xp()
    {
        var id = await _h.JoinAsync("Asha Rao");
        await QuizAsync(id, "q1", 0, 5);

        Assert.Equal(1, _h.Player(id).StreakDays);
        Assert.Equal(0, _h.Player(id).TotalXp);
    }

    [Fact]
    public async Task Daily_minutes_quest_credits_once_when_the_target_is_reached()
    {
        var id = await _h.JoinAsync("Asha Rao");
        await MinutesAsync(id, 10);
        Assert.Empty(_h.Store.Ledger);

        await MinutesAsync(id, 6);
        await MinutesAsync(id, 30);

        var credit = Assert.Single(_h.Store.Ledger);
        Assert.Equal(("daily.learn-15", "2026-10-13", 5), (credit.Reason, credit.SourceId, credit.Xp));
    }

    [Fact]
    public async Task Finish_brain_vita_is_done_by_a_first_brain_vita_brief_that_day()
    {
        var id = await _h.JoinAsync("Asha Rao");
        await BriefAsync(id, "hospitality");
        await BriefAsync(id, "brain-vita", first: false);
        Assert.Empty(_h.Store.Ledger);

        await BriefAsync(id, "brain-vita");
        await BriefAsync(id, "brain-vita");

        var credit = Assert.Single(_h.Store.Ledger);
        Assert.Equal(("daily.finish-bv", 10), (credit.Reason, credit.Xp));

        _h.Clock.Advance(TimeSpan.FromDays(1));
        await BriefAsync(id, "brain-vita");
        Assert.Equal(20, _h.Player(id).TotalXp);
    }

    [Fact]
    public async Task Quiz_pays_per_correct_answer_on_the_first_submit_only_and_80_percent_clears_the_daily()
    {
        var id = await _h.JoinAsync("Asha Rao");

        await QuizAsync(id, "q1", 8, 10);
        await QuizAsync(id, "q1", 10, 10, first: false);

        Assert.Equal(8 + 5, _h.Player(id).TotalXp);
        Assert.Contains(_h.Store.Ledger, e => e.Reason == "daily.quiz-80");
    }

    [Fact]
    public async Task Career_drivers_pays_25_per_island_and_25_on_submit_first_attempt_only()
    {
        var id = await _h.JoinAsync("Asha Rao");
        for (var island = 1; island <= 4; island++)
        {
            await IslandAsync(id, island);
        }

        await IslandAsync(id, 1);
        await SubmitAsync(id);
        await SubmitAsync(id, first: false);
        await IslandAsync(id, 2, first: false);

        Assert.Equal(100, _h.Player(id).TotalXp);
        Assert.Equal(
            new[] { "island:1", "island:2", "island:3", "submitted" },
            _h.Store.Ledger.Where(e => e.Reason == "assessment").Select(e => e.SourceId).Order(StringComparer.Ordinal));
        Assert.Equal(1, _h.Player(id).Counter("assessment.career-drivers"));
    }

    [Fact]
    public async Task One_time_rewards_pay_once()
    {
        var id = await _h.JoinAsync("Asha Rao");
        for (var i = 0; i < 2; i++)
        {
            await new FirstMovesClaimedHandler(_h.Engine(), _h.Options).HandleAsync(new FirstMovesClaimed(id) { TenantId = Tenant }, _ct);
            await new JobProfileCompletedHandler(_h.Engine(), _h.Options).HandleAsync(new JobProfileCompleted(id) { TenantId = Tenant }, _ct);
            await new ApplicationSentHandler(_h.Engine(), _h.Options).HandleAsync(new ApplicationSent(id, $"a{i}", "j1") { TenantId = Tenant }, _ct);
            await new FestBookedHandler(_h.Engine(), _h.Options).HandleAsync(new FestBooked(id, $"b{i}", "f1") { TenantId = Tenant }, _ct);
            await new ProfileTaskHandler(_h.Engine(), _h.Options).HandleAsync(new ProfileTaskCompleted(id, "photo") { TenantId = Tenant }, _ct);
            await new ProfileTaskHandler(_h.Engine(), _h.Options).HandleAsync(new ProfileTaskCompleted(id, "skills") { TenantId = Tenant }, _ct);
        }

        // BR-107 50 + BR-110 50 + BR-111 100 + BR-112 100 + BR-108 5 + BR-109 10.
        Assert.Equal(315, _h.Player(id).TotalXp);
        Assert.Contains(_h.Store.Ledger, e => (e.Reason, e.SourceId) == ("mission", "job-profile"));
        Assert.Contains(_h.Store.Ledger, e => (e.Reason, e.SourceId) == ("first-moves", "once"));
    }

    [Fact]
    public async Task Feedback_pays_at_most_once_every_30_days()
    {
        var id = await _h.JoinAsync("Asha Rao");

        await FeedbackAsync(id);
        _h.Clock.Advance(TimeSpan.FromDays(29));
        await FeedbackAsync(id);
        Assert.Equal(5, _h.Player(id).TotalXp);

        _h.Clock.Advance(TimeSpan.FromDays(1));
        await FeedbackAsync(id);
        Assert.Equal(10, _h.Player(id).TotalXp);
    }

    [Fact]
    public async Task Programme_bonus_pays_once_per_learner_and_sets_the_batch()
    {
        var id = await _h.JoinAsync("Asha Rao");
        await new ProgrammeEnrolledHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new ProgrammeEnrolled(id, "SKM-MUM-26-0417", true) { TenantId = Tenant }, _ct);
        await new ProgrammeEnrolledHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new ProgrammeEnrolled(id, "SKM-PUN-26-0001", false) { TenantId = Tenant }, _ct);

        Assert.Equal(("SKM-PUN-26-0001", 100), (_h.Player(id).Batch, _h.Player(id).TotalXp));
        Assert.Equal("once", Assert.Single(_h.Store.Ledger).SourceId);
    }

    [Fact]
    public async Task Erasure_removes_everything()
    {
        var id = await _h.JoinAsync("Asha Rao");
        await StepAsync(id, "who-dis", 1);

        var tombstones = new SkillMuni.Testing.FakeErasureTombstones();
        await new AccountErasedHandler(_h.Store, tombstones).HandleAsync(new AccountErased(id) { TenantId = Tenant }, _ct);
        Assert.Empty(_h.Store.Players);
        Assert.Empty(_h.Store.Ledger);
        Assert.Contains(id, tombstones.Erased);
    }

    [Fact]
    public async Task Levels_follow_the_ladder()
    {
        var id = await _h.JoinAsync("Asha Rao");
        await _h.GiveXpAsync(id, 1000);

        var totals = _h.Store.Events.OfType<XpAwarded>().Last();
        Assert.Equal((3, "Trailblazer", 800, 1200), (totals.Level, totals.Rank, totals.LevelStartXp, totals.NextLevelXp));
        Assert.DoesNotContain(_h.Store.Awards, a => a.BadgeId == "xp-1k");
    }

    [Fact]
    public void Xp_table_matches_brd_section_4_1()
    {
        var xp = new GamificationOptions().Xp;
        Assert.Equal(
            (20, 50, 25, 25, 1, 50, 5, 10, 50, 100, 100, 100, 5),
            (xp.QuestStep, xp.QuestComplete, xp.IslandCleared, xp.AssessmentSubmitted, xp.QuizCorrectAnswer, xp.FirstMoves, xp.ProfilePhoto,
                xp.ProfileSkills, xp.JobProfileComplete, xp.FirstApplication, xp.FirstFestBooking, xp.Programme, xp.Feedback));

        var dailies = new GamificationOptions().Dailies();
        Assert.Equal(new[] { ("finish-bv", 10), ("learn-15", 5), ("quiz-80", 5) }, dailies.Select(d => (d.Id, d.Xp)));

        // BR-103: 18 steps and 8 quests, 760 XP at most.
        var quests = new GamificationOptions().QuestBadges();
        Assert.Equal(18, quests.Sum(q => q.Parts));
        Assert.Equal(760, (quests.Sum(q => q.Parts) * xp.QuestStep) + (quests.Count * xp.QuestComplete));
    }
}

public sealed class ViewTests
{
    [Fact]
    public void Week_runs_monday_to_sunday()
    {
        var tuesday = new DateOnly(2026, 10, 13);
        var week = PlayerViews.Week(tuesday, [new DateOnly(2026, 10, 12)]);
        Assert.Equal(new[] { "done", "today", "upcoming", "upcoming", "upcoming", "upcoming", "upcoming" }, week);

        var sunday = new DateOnly(2026, 10, 18);
        Assert.Equal("missed", PlayerViews.Week(sunday, [])[0]);
        Assert.Equal(new DateOnly(2026, 10, 12), PlayerViews.Monday(sunday));
    }
}

public sealed class ValidatorTests
{
    [Fact]
    public void Showcase_needs_a_list_of_ids()
    {
        var validator = new ShowcaseRequestValidator();
        Assert.False(validator.Validate(new ShowcaseRequest(null)).IsValid);
        Assert.False(validator.Validate(new ShowcaseRequest([""])).IsValid);
        Assert.True(validator.Validate(new ShowcaseRequest([])).IsValid);
        Assert.True(validator.Validate(new ShowcaseRequest(["quest-who-dis"])).IsValid);
    }

    [Fact]
    public void Arena_scope_and_period_are_from_the_lists()
    {
        var validator = new ArenaQueryValidator();
        Assert.True(validator.Validate(new ArenaQuery(null, null)).IsValid);
        Assert.True(validator.Validate(new ArenaQuery("batch", "month")).IsValid);
        Assert.False(validator.Validate(new ArenaQuery("galaxy", "week")).IsValid);
        Assert.False(validator.Validate(new ArenaQuery("everyone", "year")).IsValid);
    }
}

public sealed class StatsAndArenaTests
{
    private const string Tenant = "skillmuni-in";
    private readonly GameHarness _h = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private async Task<Guid> PlayerWithXpAsync(string name, int xp)
    {
        var id = await _h.JoinAsync(name);
        await _h.GiveXpAsync(id, xp);
        _h.Clock.Advance(TimeSpan.FromMinutes(1));
        return id;
    }

    [Fact]
    public async Task Board_ranks_by_xp_and_ties_go_to_whoever_got_there_first()
    {
        var first = await PlayerWithXpAsync("Ananya Iyer", 180);
        var second = await PlayerWithXpAsync("Kabir Nair", 180);
        var me = await PlayerWithXpAsync("Yash Pohare", 60);

        var board = (await _h.Arena().BoardAsync(me, "everyone", "week", _ct)).Value;

        Assert.Equal(new[] { first.ToString(), second.ToString(), me.ToString() }, board.Players.Select(p => p.Id));
        Assert.True(board.Players[2].You);
        Assert.Equal((3, 60, "Kabir Nair", 180, 180), (board.Me.Rank, board.Me.Xp, board.Me.NextName, board.Me.NextXp, board.Me.TopXp));
        Assert.Equal("AI", board.Players[0].Initials);
        Assert.Equal(0, board.Rewards.Coins);
        Assert.Null(board.Rewards.Voucher);
    }

    [Fact]
    public async Task Passing_someone_shows_rank_up_once()
    {
        var kabir = await PlayerWithXpAsync("Kabir Nair", 100);
        var me = await PlayerWithXpAsync("Yash Pohare", 50);
        await _h.Arena().BoardAsync(me, "everyone", "week", _ct);

        await _h.GiveXpAsync(me, 100);

        var board = (await _h.Arena().BoardAsync(me, "everyone", "week", _ct)).Value;
        Assert.Equal(new RankUpDto(1, "Kabir"), board.RankUp);
        Assert.Null((await _h.Arena().BoardAsync(me, "everyone", "week", _ct)).Value.RankUp);
        Assert.NotEqual(Guid.Empty, kabir);
    }

    [Fact]
    public async Task Bad_scope_or_period_is_422()
    {
        var me = await _h.JoinAsync("Yash");
        Assert.Equal("invalid-scope", (await _h.Arena().BoardAsync(me, "galaxy", "week", _ct)).Error!.Code);
        Assert.Equal("invalid-period", (await _h.Arena().BoardAsync(me, "everyone", "year", _ct)).Error!.Code);
    }

    [Fact]
    public async Task Cheering_toggles_and_counts_each_player_once_for_the_badge()
    {
        var me = await _h.JoinAsync("Yash");
        var other = await _h.JoinAsync("Ananya Iyer");

        Assert.Equal(1, (await _h.Arena().CheerAsync(me, other, true, _ct)).Value.Cheers);
        Assert.Equal(0, (await _h.Arena().CheerAsync(me, other, false, _ct)).Value.Cheers);
        Assert.Equal(1, (await _h.Arena().CheerAsync(me, other, true, _ct)).Value.Cheers);

        Assert.Equal(1, _h.Player(me).Counter("cheers.given"));
        Assert.Equal("not-yourself", (await _h.Arena().CheerAsync(me, me, true, _ct)).Error!.Code);
        Assert.Equal("player-not-found", (await _h.Arena().CheerAsync(me, Guid.NewGuid(), true, _ct)).Error!.Code);
    }

    [Fact]
    public async Task One_open_challenge_per_pair()
    {
        var me = await _h.JoinAsync("Yash");
        var other = await _h.JoinAsync("Ananya Iyer");

        Assert.True((await _h.Arena().ChallengeAsync(me, other, _ct)).IsSuccess);
        Assert.Equal("challenge-open", (await _h.Arena().ChallengeAsync(me, other, _ct)).Error!.Code);
        _h.Clock.Advance(TimeSpan.FromDays(8));
        Assert.True((await _h.Arena().ChallengeAsync(me, other, _ct)).IsSuccess);
    }

    [Fact]
    public async Task Badge_library_shows_quest_progress_tiers_and_secrets()
    {
        var me = await _h.JoinAsync("Yash");
        await new QuestStepHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuestStepCompleted(me, "who-dis", 1, 2) { TenantId = Tenant }, _ct);

        var catalogue = await _h.Stats().BadgesAsync(me, _ct);

        Assert.Equal(8 + 48, catalogue.Total);
        var quest = catalogue.Badges.Single(b => b.Id == "quest-who-dis");
        Assert.Equal(("progress", "2 / 3 parts", "Main Character Badge", "assets/quests/quest-1.png"), (quest.State, quest.ProgressLabel, quest.Name, quest.Image));
        Assert.Equal("Continue quest", quest.Cta!.Label);
        var next = catalogue.Badges.Single(b => b.Id == "quest-hit-me-up");
        Assert.Equal(("locked", "Go to quests"), (next.State, next.Cta!.Label));
        Assert.Equal("Storyteller Badge", catalogue.Badges.Single(b => b.Id == "quest-work-lore").Name);
        Assert.Equal("secret", catalogue.Badges.Single(b => b.Id == "secret-quiz").State);
        Assert.Equal(new BadgeTiersDto(0, 3), catalogue.Badges.Single(b => b.Id == "streak-master").Tiers);
        Assert.Equal("locked", catalogue.Badges.Single(b => b.Id == "career-clarity").State);

        // Founding Learner's rule is met but catalogue badges aren't awarded in Phase 1.
        Assert.Equal("progress", catalogue.Badges.Single(b => b.Id == "founding-learner").State);
        Assert.Equal(0, catalogue.Earned);
    }

    [Fact]
    public async Task Showcase_accepts_up_to_three_earned_badges()
    {
        var me = await _h.JoinAsync("Yash");
        await new QuestCompletedHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuestCompleted(me, "who-dis", 1, "Main Character Badge") { TenantId = Tenant }, _ct);

        Assert.Equal("badge-not-earned", (await _h.Stats().SetShowcaseAsync(me, ["xp-1k"], _ct)).Error!.Code);
        Assert.Equal("too-many-pinned", (await _h.Stats().SetShowcaseAsync(me, ["a", "b", "c", "d"], _ct)).Error!.Code);
        var pinned = (await _h.Stats().SetShowcaseAsync(me, ["quest-who-dis"], _ct)).Value;
        Assert.True(pinned.Badges.Single(b => b.Id == "quest-who-dis").Pinned);

        var rewards = await _h.Stats().RewardsAsync(me, _ct);
        Assert.Equal("quest-who-dis", rewards.Badges.Showcase[0]!.Id);
        Assert.Equal("assets/quests/quest-1.png", rewards.Badges.Showcase[0]!.Image);
        Assert.Null(rewards.Badges.Showcase[1]);
    }

    [Fact]
    public async Task Rewards_dashboard_shows_rank_gap_daily_quests_and_an_empty_wallet()
    {
        await PlayerWithXpAsync("Aarav Shah", 120);
        var me = await PlayerWithXpAsync("Yash Pohare", 72);

        var rewards = await _h.Stats().RewardsAsync(me, _ct);

        Assert.Equal(new RankDto(2, 48, "Aarav"), rewards.Rank);
        Assert.Equal((72, 1, "Rookie", 400), (rewards.Player.Xp, rewards.Player.Level, rewards.Player.Title, rewards.Player.NextLevelXp));
        Assert.Equal(new[] { "finish-bv", "learn-15", "quiz-80" }, rewards.Quests.Items.Select(q => q.Id));
        Assert.Equal(("Resume where you left off", "/learn/brain-vita"), (rewards.Quests.Items[0].ProgressLabel, rewards.Quests.Items[0].Link));
        Assert.Equal("0 / 15 min", rewards.Quests.Items[1].ProgressLabel);
        Assert.Equal(new DateTime(2026, 10, 13, 18, 30, 0, DateTimeKind.Utc), rewards.Quests.ResetsAt);
        Assert.Equal((72, 0, 0, 0, 0), (rewards.Wallet.Xp, rewards.Wallet.Coins, rewards.Wallet.CoinsEarned, rewards.Wallet.CoinsSpent, rewards.Wallet.Credits));
    }

    [Fact]
    public async Task Ledger_lists_newest_first_with_labels()
    {
        var me = await PlayerWithXpAsync("Yash", 40);
        await new QuestStepHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuestStepCompleted(me, "who-dis", 1, 1) { TenantId = Tenant }, _ct);

        var page = await _h.Stats().LedgerAsync(me, PageRequest.Create(1, 20), _ct);
        Assert.Equal(new[] { "Quest step", "test" }, page.Items.Select(e => e.Label));
        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task Progress_is_the_quest_position_and_the_ledger_total()
    {
        var me = await _h.JoinAsync("Yash");
        var fresh = await _h.Stats().ProgressAsync(me, _ct);
        Assert.Equal(new LearnerProgressDto(0, 0, 0, 0, 1, "Rookie", 400), fresh);

        foreach (var part in new[] { 1, 2, 3 })
        {
            await new QuestStepHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuestStepCompleted(me, "who-dis", 1, part) { TenantId = Tenant }, _ct);
        }

        await new QuestCompletedHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuestCompleted(me, "who-dis", 1, "Main Character Badge") { TenantId = Tenant }, _ct);
        await new QuestStepHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuestStepCompleted(me, "hit-me-up", 2, 1) { TenantId = Tenant }, _ct);
        await _h.GiveXpAsync(me, 300);

        var progress = await _h.Stats().ProgressAsync(me, _ct);
        Assert.Equal(new LearnerProgressDto(1, 1, 60 + 50 + 20 + 300, 1, 2, "Pathfinder", 800), progress);
    }
}
