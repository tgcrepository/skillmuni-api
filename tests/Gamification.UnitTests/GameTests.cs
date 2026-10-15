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

    private Task StepAsync(Guid id, string slug, int part) =>
        new QuestStepHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuestStepCompleted(id, slug, 1, part) { TenantId = Tenant }, _ct);

    private Task CompleteAsync(Guid id, string slug, int quest = 1) =>
        new QuestCompletedHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuestCompleted(id, slug, quest, "ID Unlocked") { TenantId = Tenant }, _ct);

    private Task QuizAsync(Guid id, string quiz, int correct, int total, bool first = true) =>
        new QuizSubmittedHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuizSubmitted(id, quiz, correct, total, first) { TenantId = Tenant }, _ct);

    private Task MinutesAsync(Guid id, int minutes) =>
        new LearningActivityHandler(_h.Engine(), _h.Clock).HandleAsync(new LearningActivity(id, minutes) { TenantId = Tenant }, _ct);

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
    public async Task Events_before_registration_create_the_player()
    {
        var id = Guid.NewGuid();
        await StepAsync(id, "who-dis", 1);
        Assert.Equal(20, _h.Player(id).TotalXp);
    }

    [Fact]
    public async Task Finishing_a_quest_pays_once_and_earns_its_badge()
    {
        var id = await _h.JoinAsync("Asha Rao");

        await CompleteAsync(id, "who-dis");
        await CompleteAsync(id, "who-dis");

        Assert.Equal(50, _h.Player(id).TotalXp);
        Assert.Contains(_h.Store.Awards, a => a.BadgeId == "quest-who-dis");
        Assert.Single(_h.Store.Events.OfType<BadgeEarned>(), e => e.BadgeId == "quest-who-dis");

        // Plus Founding Learner, earned on joining in the first year.
        Assert.Equal(2, _h.Player(id).Badges);
    }

    [Fact]
    public async Task Streak_grows_on_consecutive_days_and_restarts_after_a_gap()
    {
        var id = await _h.JoinAsync("Asha Rao");
        await MinutesAsync(id, 5);
        _h.Clock.Advance(TimeSpan.FromDays(1));
        await MinutesAsync(id, 5);
        _h.Clock.Advance(TimeSpan.FromDays(1));
        await MinutesAsync(id, 5);

        Assert.Equal(3, _h.Player(id).StreakDays);
        Assert.Contains(_h.Store.Awards, a => a.BadgeId == "streak-starter");

        _h.Clock.Advance(TimeSpan.FromDays(3));
        await MinutesAsync(id, 5);
        Assert.Equal((1, 3), (_h.Player(id).StreakDays, _h.Player(id).BestStreak));
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
    public async Task Quiz_pays_per_correct_answer_on_the_first_check_only()
    {
        var id = await _h.JoinAsync("Asha Rao");

        await QuizAsync(id, "q1", 8, 10);
        await QuizAsync(id, "q1", 10, 10, first: false);

        Assert.Equal(8 + 5, _h.Player(id).TotalXp);
        Assert.Contains(_h.Store.Awards, a => a.BadgeId == "quiz-starter");
        Assert.DoesNotContain(_h.Store.Awards, a => a.BadgeId == "perfect-score");
    }

    [Fact]
    public async Task A_perfect_quiz_earns_perfect_score()
    {
        var id = await _h.JoinAsync("Asha Rao");
        await QuizAsync(id, "q2", 5, 5);
        Assert.Contains(_h.Store.Awards, a => a.BadgeId == "perfect-score");
    }

    [Fact]
    public async Task Thousand_xp_earns_the_1k_club_and_levels_follow_the_ladder()
    {
        var id = await _h.JoinAsync("Asha Rao");
        _h.Settings.Xp.ClaimConfirmed = 1000;

        await new ClaimConfirmedHandler(_h.Engine(), _h.Options).HandleAsync(new ClaimConfirmed(Guid.NewGuid(), id) { TenantId = Tenant }, _ct);

        Assert.Contains(_h.Store.Awards, a => a.BadgeId == "xp-1k");
        var totals = _h.Store.Events.OfType<XpAwarded>().Last();
        Assert.Equal((3, "Trailblazer", 1200), (totals.Level, totals.Rank, totals.NextLevelXp));
    }

    [Fact]
    public async Task Feedback_pays_only_when_learner_says_it_earns()
    {
        var id = await _h.JoinAsync("Asha Rao");
        var handler = new FeedbackSentHandler(_h.Engine(), _h.Options);

        await handler.HandleAsync(new FeedbackSent(id, 5, true) { TenantId = Tenant }, _ct);
        await new FeedbackSentHandler(_h.Engine(), _h.Options).HandleAsync(new FeedbackSent(id, 4, false) { TenantId = Tenant }, _ct);

        Assert.Equal(5, _h.Player(id).TotalXp);
    }

    [Fact]
    public async Task Programme_sets_the_batch_and_erasure_removes_everything()
    {
        var id = await _h.JoinAsync("Asha Rao");
        await new ProgrammeEnrolledHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new ProgrammeEnrolled(id, "SKM-MUM-26-0417", true) { TenantId = Tenant }, _ct);
        Assert.Equal(("SKM-MUM-26-0417", 100), (_h.Player(id).Batch, _h.Player(id).TotalXp));

        await new AccountErasedHandler(_h.Store).HandleAsync(new AccountErased(id) { TenantId = Tenant }, _ct);
        Assert.Empty(_h.Store.Players);
        Assert.Empty(_h.Store.Ledger);
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

public sealed class StatsAndArenaTests
{
    private const string Tenant = "skillmuni-in";
    private readonly GameHarness _h = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private async Task<Guid> PlayerWithXpAsync(string name, int xp)
    {
        var id = await _h.JoinAsync(name);
        _h.Settings.Xp.ClaimConfirmed = xp;
        await new ClaimConfirmedHandler(_h.Engine(), _h.Options).HandleAsync(new ClaimConfirmed(Guid.NewGuid(), id) { TenantId = Tenant }, _ct);
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
    }

    [Fact]
    public async Task Passing_someone_shows_rank_up_once()
    {
        var kabir = await PlayerWithXpAsync("Kabir Nair", 100);
        var me = await PlayerWithXpAsync("Yash Pohare", 50);
        await _h.Arena().BoardAsync(me, "everyone", "week", _ct);

        _h.Settings.Xp.ClaimConfirmed = 100;
        await new ClaimConfirmedHandler(_h.Engine(), _h.Options).HandleAsync(new ClaimConfirmed(Guid.NewGuid(), me) { TenantId = Tenant }, _ct);

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
        Assert.Equal(("progress", "2 / 3 parts"), (quest.State, quest.ProgressLabel));
        Assert.Equal("secret", catalogue.Badges.Single(b => b.Id == "secret-quiz").State);
        Assert.Equal(new BadgeTiersDto(0, 3), catalogue.Badges.Single(b => b.Id == "streak-master").Tiers);
        Assert.Equal("locked", catalogue.Badges.Single(b => b.Id == "career-clarity").State);
    }

    [Fact]
    public async Task Showcase_accepts_up_to_three_earned_badges()
    {
        var me = await _h.JoinAsync("Yash");
        await new QuestCompletedHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuestCompleted(me, "who-dis", 1, "ID Unlocked") { TenantId = Tenant }, _ct);

        Assert.Equal("badge-not-earned", (await _h.Stats().SetShowcaseAsync(me, ["xp-1k"], _ct)).Error!.Code);
        Assert.Equal("too-many-pinned", (await _h.Stats().SetShowcaseAsync(me, ["a", "b", "c", "d"], _ct)).Error!.Code);
        var pinned = (await _h.Stats().SetShowcaseAsync(me, ["quest-who-dis"], _ct)).Value;
        Assert.True(pinned.Badges.Single(b => b.Id == "quest-who-dis").Pinned);

        var rewards = await _h.Stats().RewardsAsync(me, _ct);
        Assert.Equal("quest-who-dis", rewards.Badges.Showcase[0]!.Id);
        Assert.Null(rewards.Badges.Showcase[1]);
    }

    [Fact]
    public async Task Rewards_dashboard_shows_rank_gap_and_daily_quests()
    {
        await PlayerWithXpAsync("Aarav Shah", 120);
        var me = await PlayerWithXpAsync("Yash Pohare", 72);

        var rewards = await _h.Stats().RewardsAsync(me, _ct);

        Assert.Equal(new RankDto(2, 48, "Aarav"), rewards.Rank);
        Assert.Equal((72, 1, "Rookie", 400), (rewards.Player.Xp, rewards.Player.Level, rewards.Player.Title, rewards.Player.NextLevelXp));
        Assert.Equal(3, rewards.Quests.Items.Count);
        Assert.Equal("0 / 15 min", rewards.Quests.Items[0].ProgressLabel);
        Assert.Equal(new DateTime(2026, 10, 13, 18, 30, 0, DateTimeKind.Utc), rewards.Quests.ResetsAt);
        Assert.Equal(0, rewards.Wallet.Coins);
    }

    [Fact]
    public async Task Ledger_lists_newest_first_with_labels()
    {
        var me = await PlayerWithXpAsync("Yash", 40);
        await new QuestStepHandler(_h.Engine(), _h.Clock, _h.Options).HandleAsync(new QuestStepCompleted(me, "who-dis", 1, 1) { TenantId = Tenant }, _ct);

        var page = await _h.Stats().LedgerAsync(me, PageRequest.Create(1, 20), _ct);
        Assert.Equal(new[] { "Quest step", "Institute profile confirmed" }, page.Items.Select(e => e.Label));
        Assert.Equal(2, page.Total);
    }
}
