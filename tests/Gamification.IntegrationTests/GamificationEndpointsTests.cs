using System.Net;
using System.Text.Json;
using Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;

namespace Gamification.IntegrationTests;

[Collection(GamificationApiCollection.Name)]
public sealed class GamificationEndpointsTests(GamificationApiFactory api)
{
    private static object Step(Guid id, string slug, int part, bool paid = true) =>
        new { learnerId = id, slug, quest = 1, part, paid, tenantId = TestTokens.Tenant };

    [Fact]
    public async Task Quest_events_pay_once_and_show_in_ledger_and_rewards()
    {
        var me = await api.JoinAsync("Yash", "Pohare");
        var stepEvent = Guid.NewGuid();

        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("learner.quest.step-completed", Step(me, "who-dis", 1), stepEvent));
        Assert.Equal(DispatchOutcome.Duplicate, await api.DeliverAsync("learner.quest.step-completed", Step(me, "who-dis", 1), stepEvent));
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("learner.quest.step-completed", Step(me, "who-dis", 1)));
        await api.DeliverAsync("learner.quest.step-completed", Step(me, "who-dis", 2, paid: false));
        await api.DeliverAsync("learner.quest.completed", new { learnerId = me, slug = "who-dis", quest = 1, badge = "Main Character Badge", tenantId = TestTokens.Tenant });

        var client = api.ClientFor(me);
        var ledger = await (await client.GetAsync("/api/v1/me/xp/ledger")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(2, ledger.GetProperty("total").GetInt32());

        var rewards = await (await client.GetAsync("/api/v1/stats/rewards")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(70, rewards.GetProperty("player").GetProperty("xp").GetInt32());
        Assert.Equal("Yash Pohare", rewards.GetProperty("player").GetProperty("name").GetString());

        // Quest XP doesn't make a streak day (§4.4: only a first quiz submit does).
        Assert.Equal(0, rewards.GetProperty("streak").GetProperty("days").GetInt32());
        Assert.Equal(7, rewards.GetProperty("streak").GetProperty("week").GetArrayLength());
        var wallet = rewards.GetProperty("wallet");
        Assert.Equal((70, 0, 0, 0, 0), (wallet.GetProperty("xp").GetInt32(), wallet.GetProperty("coins").GetInt32(), wallet.GetProperty("coinsEarned").GetInt32(), wallet.GetProperty("coinsSpent").GetInt32(), wallet.GetProperty("credits").GetInt32()));
    }

    [Fact]
    public async Task Progress_comes_from_the_ledger()
    {
        var me = await api.JoinAsync("Yash", "Pohare");
        await api.DeliverAsync("learner.quest.step-completed", Step(me, "who-dis", 1));
        await api.DeliverAsync("assessment.island.cleared", new { learnerId = me, attempt = 1, island = 1, firstAttempt = true, tenantId = TestTokens.Tenant });
        await api.DeliverAsync("learner.job-profile.completed", new { learnerId = me, tenantId = TestTokens.Tenant });
        await api.DeliverAsync("learner.programme.enrolled", new { learnerId = me, programmeCode = "SKM-1", paysStipend = false, tenantId = TestTokens.Tenant });
        await api.DeliverAsync("learner.programme.enrolled", new { learnerId = me, programmeCode = "SKM-2", paysStipend = false, tenantId = TestTokens.Tenant });

        var progress = await (await api.ClientFor(me).GetAsync("/api/v1/me/progress")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(0, progress.GetProperty("completed").GetInt32());
        Assert.Equal(1, progress.GetProperty("partsDone").GetInt32());
        Assert.Equal(20 + 25 + 50 + 100, progress.GetProperty("xp").GetInt32());
        Assert.Equal(0, progress.GetProperty("badges").GetInt32());
        Assert.Equal((1, "Rookie", 400), (progress.GetProperty("level").GetInt32(), progress.GetProperty("rank").GetString(), progress.GetProperty("target").GetInt32()));
    }

    [Fact]
    public async Task Arena_board_and_cheers()
    {
        var leader = await api.JoinAsync("Ananya", "Iyer");
        var me = await api.JoinAsync("Yash", "Pohare");
        await api.DeliverAsync("learner.quest.step-completed", Step(leader, "who-dis", 1));
        await api.DeliverAsync("learner.quest.step-completed", Step(leader, "who-dis", 2));
        await api.DeliverAsync("learner.quest.step-completed", Step(me, "who-dis", 1));

        var client = api.ClientFor(me);
        var board = await (await client.GetAsync("/api/v1/arena?scope=everyone&period=week")).JsonAsync(HttpStatusCode.OK);
        var players = board.GetProperty("players").EnumerateArray().ToList();
        var leaderRow = players.First(p => p.GetProperty("id").GetString() == leader.ToString());
        var myRow = players.First(p => p.GetProperty("you").GetBoolean());
        Assert.True(leaderRow.GetProperty("rank").GetInt32() < myRow.GetProperty("rank").GetInt32());
        Assert.Equal(myRow.GetProperty("rank").GetInt32(), board.GetProperty("me").GetProperty("rank").GetInt32());
        Assert.False(board.TryGetProperty("streakBonus", out _));
        Assert.Equal(0, board.GetProperty("rewards").GetProperty("coins").GetInt32());
        Assert.Equal(JsonValueKind.Null, board.GetProperty("rewards").GetProperty("voucher").ValueKind);

        var cheer = await (await client.PostJsonAsync($"/api/v1/arena/players/{leader}/cheer", new { on = true })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(1, cheer.GetProperty("cheers").GetInt32());
        Assert.Equal("not-yourself", await (await client.PostJsonAsync($"/api/v1/arena/players/{me}/cheer", new { on = true })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync($"/api/v1/arena/players/{leader}/challenge", new { })).StatusCode);
        Assert.Equal("challenge-open", await (await client.PostJsonAsync($"/api/v1/arena/players/{leader}/challenge", new { })).ProblemCodeAsync(HttpStatusCode.Conflict));

        var friends = await (await client.GetAsync("/api/v1/arena?scope=friends&period=all")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(2, friends.GetProperty("players").GetArrayLength());
        Assert.Equal("validation", await (await client.GetAsync("/api/v1/arena?scope=galaxy")).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation", await (await client.GetAsync("/api/v1/arena?period=year")).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Badges_and_showcase()
    {
        var me = await api.JoinAsync("Yash", "Pohare");
        await api.DeliverAsync("learner.quest.completed", new { learnerId = me, slug = "who-dis", quest = 1, badge = "Main Character Badge", tenantId = TestTokens.Tenant });
        var client = api.ClientFor(me);

        var catalogue = await (await client.GetAsync("/api/v1/badges")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(56, catalogue.GetProperty("total").GetInt32());
        Assert.Equal(1, catalogue.GetProperty("earned").GetInt32());
        var quest = catalogue.GetProperty("badges").EnumerateArray().First(b => b.GetProperty("id").GetString() == "quest-who-dis");
        Assert.Equal(("earned", "Main Character Badge"), (quest.GetProperty("state").GetString(), quest.GetProperty("name").GetString()));
        Assert.Equal("assets/quests/quest-1.png", quest.GetProperty("image").GetString());

        var pinned = await (await client.PutJsonAsync("/api/v1/badges/showcase", new { ids = new[] { "quest-who-dis" } })).JsonAsync(HttpStatusCode.OK);
        Assert.True(pinned.GetProperty("badges").EnumerateArray().First(b => b.GetProperty("id").GetString() == "quest-who-dis").GetProperty("pinned").GetBoolean());
        Assert.Equal("badge-not-earned", await (await client.PutJsonAsync("/api/v1/badges/showcase", new { ids = new[] { "xp-1k" } })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation", await (await client.PutJsonAsync("/api/v1/badges/showcase", new { ids = (string[]?)null })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation", await (await client.PutJsonAsync("/api/v1/badges/showcase", new { })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Daily_quests_and_streak()
    {
        var me = await api.JoinAsync("Yash", "Pohare");
        await api.DeliverAsync("learning.activity.recorded", new { learnerId = me, minutes = 20, tenantId = TestTokens.Tenant });
        await api.DeliverAsync("learning.brief.completed", new { learnerId = me, briefId = "bv-1", moduleSlug = "verbal", courseSlug = "brain-vita", firstCompletion = true, tenantId = TestTokens.Tenant });
        var client = api.ClientFor(me);

        var daily = await (await client.GetAsync("/api/v1/me/daily-quests")).JsonAsync(HttpStatusCode.OK);
        var items = daily.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new[] { "finish-bv", "learn-15", "quiz-80" }, items.Select(q => q.GetProperty("id").GetString()));
        Assert.True(items[0].GetProperty("done").GetBoolean());
        Assert.True(items[1].GetProperty("done").GetBoolean());

        var streak = await (await client.GetAsync("/api/v1/me/streak")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(0, streak.GetProperty("days").GetInt32());

        await api.DeliverAsync("learning.quiz.submitted", new { learnerId = me, quizId = "bv-1-quiz", briefId = "bv-1", moduleSlug = "verbal", correct = 4, total = 5, firstAttempt = true, tenantId = TestTokens.Tenant });
        streak = await (await client.GetAsync("/api/v1/me/streak")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(1, streak.GetProperty("days").GetInt32());
        Assert.False(streak.TryGetProperty("bonus", out _));
    }

    [Fact]
    public async Task Erasure_removes_the_player()
    {
        var me = await api.JoinAsync("Yash", "Pohare");
        await api.DeliverAsync("learner.quest.step-completed", Step(me, "who-dis", 1));

        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("identity.learner.erased", new { accountId = me, tenantId = TestTokens.Tenant }));

        await using var scope = api.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        Assert.False(await db.Players.AnyAsync(p => p.Id == me));
        Assert.False(await db.XpEntries.AnyAsync(e => e.LearnerId == me));
    }

    [Fact]
    public async Task Two_updates_of_one_player_at_once_cannot_overwrite_each_other()
    {
        var me = await api.JoinAsync("Race", "Condition");

        await using var first = api.Services.CreateAsyncScope();
        await using var second = api.Services.CreateAsyncScope();
        first.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        second.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        var a = first.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var b = second.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var playerA = await a.Players.SingleAsync(p => p.Id == me);
        var playerB = await b.Players.SingleAsync(p => p.Id == me);

        playerA.Count("quests.done.work-lore", 1, DateTime.UtcNow);
        await a.SaveChangesAsync();
        playerB.Count("quests.done.pick-your-arc", 1, DateTime.UtcNow.AddSeconds(1));

        // The loser fails (its event is retried and re-reads the player) instead of dropping the first counter.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task No_session_is_401() =>
        Assert.Equal("no-session", await (await api.CreateClient().GetAsync("/api/v1/arena")).ProblemCodeAsync(HttpStatusCode.Unauthorized));
}
