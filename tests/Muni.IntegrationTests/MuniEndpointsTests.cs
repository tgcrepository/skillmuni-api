using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Muni.Application;
using Muni.Infrastructure.Persistence;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;

namespace Muni.IntegrationTests;

[Collection(MuniApiCollection.Name)]
public sealed class MuniEndpointsTests(MuniApiFactory api)
{
    private async Task<Guid> JoinAsync(string first, string last)
    {
        var id = Guid.NewGuid();
        await api.DeliverAsync("identity.learner.registered", new { accountId = id, firstName = first, lastName = last, market = "IN", tenantId = TestTokens.Tenant });
        return id;
    }

    [Fact]
    public async Task Welcome_reflects_xp_from_gamification()
    {
        var me = await JoinAsync("Yash", "Pohare");
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("gamification.xp.awarded", new
        {
            learnerId = me,
            totalXp = 450,
            level = 2,
            rank = "Pathfinder",
            levelStartXp = 400,
            nextLevelXp = 800,
            badges = 1,
            streakDays = 2,
            tenantId = TestTokens.Tenant,
        }));

        var welcome = await (await api.ClientFor(me).GetAsync("/api/v1/muni/welcome")).JsonAsync(HttpStatusCode.OK);

        Assert.Equal("Yash", welcome.GetProperty("firstName").GetString());
        Assert.Equal("Level 2 · Pathfinder, 450 XP. Ask me anything about Skillmuni, or start with what I’d do right now:", welcome.GetProperty("intro").GetString());
        var suggestions = welcome.GetProperty("suggestions").EnumerateArray().ToList();
        Assert.Equal(3, suggestions.Count);
        Assert.Equal("drivers", suggestions[0].GetProperty("id").GetString());
        Assert.Equal("/assessments/career", suggestions[0].GetProperty("link").GetString());
        Assert.False(suggestions[0].TryGetProperty("intent", out _));
        Assert.Equal(4, welcome.GetProperty("topics").GetArrayLength());
        Assert.Equal(0, welcome.GetProperty("unread").GetInt32());
        Assert.Equal(JsonValueKind.Null, welcome.GetProperty("nudge").ValueKind);
    }

    [Fact]
    public async Task Messages_reply_with_cards_and_land_in_history()
    {
        var me = await JoinAsync("Asha", "Rao");
        var client = api.ClientFor(me, "Asha");

        var greeting = await (await client.PostJsonAsync("/api/v1/muni/messages", new { text = "hi" })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("text", greeting.GetProperty("blocks")[0].GetProperty("kind").GetString());
        Assert.StartsWith("Heyyy Asha", greeting.GetProperty("blocks")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.True(Guid.TryParse(greeting.GetProperty("id").GetString(), out _));

        var profile = await (await client.PostJsonAsync("/api/v1/muni/messages", new { text = "Show my profile", intent = "Show my profile" })).JsonAsync(HttpStatusCode.OK);
        var card = profile.GetProperty("blocks")[1];
        Assert.Equal("profile", card.GetProperty("kind").GetString());
        Assert.Equal("AR", card.GetProperty("initials").GetString());
        Assert.Equal("Asha Rao", card.GetProperty("name").GetString());
        Assert.Equal("y••••@thegamification.com", card.GetProperty("fields")[0].GetProperty("value").GetString());
        Assert.True(card.GetProperty("fields")[0].GetProperty("masked").GetBoolean());
        Assert.Equal(50, card.GetProperty("completeness").GetInt32());
        Assert.Equal(3, profile.GetProperty("chips").GetArrayLength());

        var jobs = await (await client.PostJsonAsync("/api/v1/muni/messages", new { text = "Jobs for me", intent = "Jobs for me" })).JsonAsync(HttpStatusCode.OK);
        var list = jobs.GetProperty("blocks")[1];
        Assert.Equal("list", list.GetProperty("kind").GetString());
        Assert.False(list.GetProperty("items")[0].TryGetProperty("tag", out _));

        var history = await (await client.GetAsync("/api/v1/muni/history")).JsonAsync(HttpStatusCode.OK);
        var conversation = Assert.Single(history.EnumerateArray());
        Assert.Equal("hi", conversation.GetProperty("title").GetString());
        Assert.Equal("Today", conversation.GetProperty("when").GetString());
        Assert.Equal(6, conversation.GetProperty("messages").GetArrayLength());
        Assert.Equal("me", conversation.GetProperty("messages")[0].GetProperty("from").GetString());
        Assert.Equal("muni", conversation.GetProperty("messages")[1].GetProperty("from").GetString());
    }

    [Fact]
    public async Task Empty_question_is_422()
    {
        var client = api.ClientFor(Guid.NewGuid());

        Assert.Equal("validation", await (await client.PostJsonAsync("/api/v1/muni/messages", new { text = " " })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Reveal_returns_the_private_value_uncached()
    {
        var client = api.ClientFor(Guid.NewGuid());

        var response = await client.PostJsonAsync("/api/v1/muni/reveal", new { field = "mobile" });
        var body = await response.JsonAsync(HttpStatusCode.OK);
        Assert.Equal("+91 98765 40697", body.GetProperty("value").GetString());
        Assert.True(response.Headers.CacheControl?.NoStore);

        Assert.Equal("validation", await (await client.PostJsonAsync("/api/v1/muni/reveal", new { field = "aadhaar" })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Rating_is_204_and_stored()
    {
        var me = Guid.NewGuid();
        var client = api.ClientFor(me);
        var reply = await (await client.PostJsonAsync("/api/v1/muni/messages", new { text = "how do i earn xp" })).JsonAsync(HttpStatusCode.OK);
        var id = reply.GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutJsonAsync($"/api/v1/muni/messages/{id}/rating", new { helpful = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutJsonAsync("/api/v1/muni/messages/local-7/rating", new { helpful = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutJsonAsync("/api/v1/muni/messages/local-7/rating", new { helpful = (bool?)null })).StatusCode);

        await using var scope = api.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<MuniDbContext>();
        var ratings = await db.Ratings.Where(r => r.LearnerId == me).ToListAsync();
        Assert.True(Assert.Single(ratings).Helpful);
        Assert.Equal(id, ratings[0].MessageRef);
    }

    [Fact]
    public async Task Erasure_and_retention_delete_history()
    {
        var me = await JoinAsync("Ravi", "Kumar");
        var client = api.ClientFor(me, "Ravi");
        await client.PostJsonAsync("/api/v1/muni/messages", new { text = "hello" });

        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("identity.learner.erased", new { accountId = me, tenantId = TestTokens.Tenant }));
        Assert.Equal(0, (await (await client.GetAsync("/api/v1/muni/history")).JsonAsync(HttpStatusCode.OK)).GetArrayLength());

        // An XP event published before the erasure but delivered after it must not bring the learner back.
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("gamification.xp.awarded", new
        {
            learnerId = me,
            totalXp = 5,
            level = 1,
            rank = "Rookie",
            levelStartXp = 0,
            nextLevelXp = 400,
            badges = 0,
            streakDays = 0,
            tenantId = TestTokens.Tenant,
        }));
        var welcome = await (await client.GetAsync("/api/v1/muni/welcome")).JsonAsync(HttpStatusCode.OK);
        Assert.StartsWith("Level 1 · Rookie, 0 XP.", welcome.GetProperty("intro").GetString(), StringComparison.Ordinal);

        // A token issued before the erasure still gets answers, but nothing is kept.
        await client.PostJsonAsync("/api/v1/muni/messages", new { text = "hello again" });
        Assert.Equal(0, (await (await client.GetAsync("/api/v1/muni/history")).JsonAsync(HttpStatusCode.OK)).GetArrayLength());

        var other = Guid.NewGuid();
        await api.ClientFor(other).PostJsonAsync("/api/v1/muni/messages", new { text = "hello" });
        await using var scope = api.Services.CreateAsyncScope();
        var deleted = await scope.ServiceProvider.GetRequiredService<IChatRetention>().DeleteOlderThanAsync(DateTime.UtcNow.AddMinutes(1), 1000, CancellationToken.None);
        Assert.True(deleted >= 2);
        Assert.Equal(0, (await (await api.ClientFor(other).GetAsync("/api/v1/muni/history")).JsonAsync(HttpStatusCode.OK)).GetArrayLength());
    }

    [Fact]
    public async Task No_session_is_401() =>
        Assert.Equal("no-session", await (await api.CreateClient().GetAsync("/api/v1/muni/welcome")).ProblemCodeAsync(HttpStatusCode.Unauthorized));
}
