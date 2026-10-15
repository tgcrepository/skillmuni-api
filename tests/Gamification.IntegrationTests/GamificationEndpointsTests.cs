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
    private static object Step(Guid id, string slug, int part) =>
        new { learnerId = id, slug, quest = 1, part, tenantId = TestTokens.Tenant };

    [Fact]
    public async Task Quest_events_pay_once_and_show_in_ledger_and_rewards()
    {
        var me = await api.JoinAsync("Yash", "Pohare");
        var stepEvent = Guid.NewGuid();

        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("learner.quest.step-completed", Step(me, "who-dis", 1), stepEvent));
        Assert.Equal(DispatchOutcome.Duplicate, await api.DeliverAsync("learner.quest.step-completed", Step(me, "who-dis", 1), stepEvent));
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("learner.quest.step-completed", Step(me, "who-dis", 1)));
        await api.DeliverAsync("learner.quest.completed", new { learnerId = me, slug = "who-dis", quest = 1, badge = "ID Unlocked", tenantId = TestTokens.Tenant });

        var client = api.ClientFor(me);
        var ledger = await (await client.GetAsync("/api/v1/me/xp/ledger")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(2, ledger.GetProperty("total").GetInt32());

        var rewards = await (await client.GetAsync("/api/v1/stats/rewards")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(70, rewards.GetProperty("player").GetProperty("xp").GetInt32());
        Assert.Equal("Yash Pohare", rewards.GetProperty("player").GetProperty("name").GetString());
        Assert.Equal(1, rewards.GetProperty("streak").GetProperty("days").GetInt32());
        Assert.Equal(7, rewards.GetProperty("streak").GetProperty("week").GetArrayLength());
    }

    [Fact]
    public async Task Arena_board_and_cheers()
    {
        var leader = await api.JoinAsync("Ananya", "Iyer");
        var me = await api.JoinAsync("Yash", "Pohare");
        await api.DeliverAsync("identity.claim.confirmed", new { claimId = Guid.NewGuid(), accountId = leader, tenantId = TestTokens.Tenant });
        await api.DeliverAsync("learner.quest.step-completed", Step(me, "who-dis", 1));

        var client = api.ClientFor(me);
        var board = await (await client.GetAsync("/api/v1/arena?scope=everyone&period=week")).JsonAsync(HttpStatusCode.OK);
        var players = board.GetProperty("players").EnumerateArray().ToList();
        var leaderRow = players.First(p => p.GetProperty("id").GetString() == leader.ToString());
        var myRow = players.First(p => p.GetProperty("you").GetBoolean());
        Assert.True(leaderRow.GetProperty("rank").GetInt32() < myRow.GetProperty("rank").GetInt32());
        Assert.Equal(myRow.GetProperty("rank").GetInt32(), board.GetProperty("me").GetProperty("rank").GetInt32());

        var cheer = await (await client.PostJsonAsync($"/api/v1/arena/players/{leader}/cheer", new { on = true })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(1, cheer.GetProperty("cheers").GetInt32());
        Assert.Equal("not-yourself", await (await client.PostJsonAsync($"/api/v1/arena/players/{me}/cheer", new { on = true })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync($"/api/v1/arena/players/{leader}/challenge", new { })).StatusCode);
        Assert.Equal("challenge-open", await (await client.PostJsonAsync($"/api/v1/arena/players/{leader}/challenge", new { })).ProblemCodeAsync(HttpStatusCode.Conflict));

        var friends = await (await client.GetAsync("/api/v1/arena?scope=friends&period=all")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(2, friends.GetProperty("players").GetArrayLength());
        Assert.Equal("invalid-scope", await (await client.GetAsync("/api/v1/arena?scope=galaxy")).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Badges_and_showcase()
    {
        var me = await api.JoinAsync("Yash", "Pohare");
        await api.DeliverAsync("learner.quest.completed", new { learnerId = me, slug = "who-dis", quest = 1, badge = "ID Unlocked", tenantId = TestTokens.Tenant });
        var client = api.ClientFor(me);

        var catalogue = await (await client.GetAsync("/api/v1/badges")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(56, catalogue.GetProperty("total").GetInt32());
        var quest = catalogue.GetProperty("badges").EnumerateArray().First(b => b.GetProperty("id").GetString() == "quest-who-dis");
        Assert.Equal("earned", quest.GetProperty("state").GetString());

        var pinned = await (await client.PutJsonAsync("/api/v1/badges/showcase", new { ids = new[] { "quest-who-dis" } })).JsonAsync(HttpStatusCode.OK);
        Assert.True(pinned.GetProperty("badges").EnumerateArray().First(b => b.GetProperty("id").GetString() == "quest-who-dis").GetProperty("pinned").GetBoolean());
        Assert.Equal("badge-not-earned", await (await client.PutJsonAsync("/api/v1/badges/showcase", new { ids = new[] { "xp-1k" } })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Daily_quests_and_streak()
    {
        var me = await api.JoinAsync("Yash", "Pohare");
        await api.DeliverAsync("learning.activity.recorded", new { learnerId = me, minutes = 20, tenantId = TestTokens.Tenant });
        var client = api.ClientFor(me);

        var daily = await (await client.GetAsync("/api/v1/me/daily-quests")).JsonAsync(HttpStatusCode.OK);
        var learn = daily.GetProperty("items").EnumerateArray().First(q => q.GetProperty("id").GetString() == "learn-15");
        Assert.True(learn.GetProperty("done").GetBoolean());

        var streak = await (await client.GetAsync("/api/v1/me/streak")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(1, streak.GetProperty("days").GetInt32());
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
    public async Task No_session_is_401() =>
        Assert.Equal("no-session", await (await api.CreateClient().GetAsync("/api/v1/arena")).ProblemCodeAsync(HttpStatusCode.Unauthorized));
}
