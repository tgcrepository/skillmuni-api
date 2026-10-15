using System.Net;
using Careers.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;

namespace Careers.IntegrationTests;

/// <summary>API-090–096 and API-104–109 against MySQL, with the events that feed them.</summary>
[Collection(CareersApiCollection.Name)]
public sealed class LaunchpadEndpointsTests(CareersApiFactory api)
{
    private object Submitted(Guid learner) => new
    {
        learnerId = learner,
        attempt = 1,
        firstAttempt = true,
        archetypeName = "The People-First Service Builder",
        archetypeSummary = "You light up when you help people.",
        topDrivers = new[] { "People", "Helping", "Structure" },
        tenantId = TestTokens.Tenant,
    };

    [Fact]
    public async Task Cards_for_a_new_learner()
    {
        var client = api.ClientFor(Guid.NewGuid());

        var readiness = await (await client.GetAsync("/api/v1/launchpad/readiness")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(0, readiness.GetProperty("level").GetInt32());
        Assert.Equal("On the launchpad", readiness.GetProperty("levelName").GetString());
        Assert.Equal(4, readiness.GetProperty("missions").GetArrayLength());

        var profile = await (await client.GetAsync("/api/v1/launchpad/profile")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(0, profile.GetProperty("percent").GetInt32());

        var assessment = await (await client.GetAsync("/api/v1/launchpad/assessment")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("not-started", assessment.GetProperty("status").GetString());

        var jobs = await (await client.GetAsync("/api/v1/launchpad/jobs")).JsonAsync(HttpStatusCode.OK);
        Assert.False(jobs.GetProperty("locked").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, jobs.GetProperty("top").ValueKind);

        var fest = await (await client.GetAsync("/api/v1/launchpad/fest")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("tech-bpo-pune", fest.GetProperty("fest").GetProperty("id").GetString());
        Assert.Equal(47, fest.GetProperty("fest").GetProperty("daysAway").GetInt32());
        Assert.False(fest.GetProperty("alertsOn").GetBoolean());

        var sectors = await (await client.GetAsync("/api/v1/launchpad/sectors")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(14, sectors.GetArrayLength());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, sectors[0].GetProperty("fit").ValueKind);

        var alerts = await (await client.PutJsonAsync("/api/v1/launchpad/fest/alerts", new { on = true })).JsonAsync(HttpStatusCode.OK);
        Assert.True(alerts.GetProperty("alertsOn").GetBoolean());
    }

    [Fact]
    public async Task Events_tick_missions_and_fill_the_assessment_card()
    {
        var me = Guid.NewGuid();
        var submitted = Guid.NewGuid();
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("assessment.attempt.submitted", Submitted(me), submitted));
        Assert.Equal(DispatchOutcome.Duplicate, await api.DeliverAsync("assessment.attempt.submitted", Submitted(me), submitted));
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("learner.job-profile.completed", new { learnerId = me, tenantId = TestTokens.Tenant }));
        var client = api.ClientFor(me);

        var readiness = await (await client.GetAsync("/api/v1/launchpad/readiness")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(2, readiness.GetProperty("level").GetInt32());
        Assert.Equal("Lift-off", readiness.GetProperty("levelName").GetString());
        Assert.Equal(43, readiness.GetProperty("percent").GetInt32());

        var card = await (await client.GetAsync("/api/v1/launchpad/assessment")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("done", card.GetProperty("status").GetString());
        Assert.Equal("🤝", card.GetProperty("archetype").GetProperty("emoji").GetString());
        Assert.Equal("Helping", card.GetProperty("archetype").GetProperty("traits")[1].GetString());

        var sectors = await (await client.GetAsync("/api/v1/launchpad/sectors")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(94, sectors[0].GetProperty("fit").GetInt32());
    }

    [Fact]
    public async Task Matches_after_publishing_or_a_confirmed_claim()
    {
        var me = Guid.NewGuid();
        api.Learners.SetPercent(me, 100);
        api.Learners.SetProfile(me, new LearnerCareerProfile("Yash Pohare", null, "Mumbai", "Maharashtra", true, [new("Customer service", "assessed"), new("Hindi + English", "level"), new("Typing", "declared")]));
        var client = api.ClientFor(me);

        var preview = await (await client.GetAsync("/api/v1/me/matches/preview")).JsonAsync(HttpStatusCode.OK);
        Assert.True(preview.GetProperty("count").GetInt32() > 0);
        Assert.Equal(2, preview.GetProperty("top").GetArrayLength());

        var hidden = await (await client.GetAsync("/api/v1/me/matches")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(0, hidden.GetProperty("jobs").GetArrayLength());
        Assert.Equal(100, hidden.GetProperty("profileComplete").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync("/api/v1/me/profile/publish", new { discoverable = true, invites = true })).StatusCode);
        var board = await (await client.GetAsync("/api/v1/me/matches")).JsonAsync(HttpStatusCode.OK);
        Assert.True(board.GetProperty("visible").GetBoolean());
        Assert.True(board.GetProperty("jobs").GetArrayLength() > 0);
        var top = board.GetProperty("jobs")[0];
        Assert.Equal("recommended", top.GetProperty("status").GetString());
        var id = top.GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync($"/api/v1/me/matches/{id}/save", new { saved = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync($"/api/v1/me/matches/{id}/apply", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync($"/api/v1/me/matches/{id}/accept", new { })).StatusCode);
        var after = await (await client.GetAsync("/api/v1/me/matches")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("accepted", after.GetProperty("jobs").EnumerateArray().First(j => j.GetProperty("id").GetString() == id).GetProperty("status").GetString());
        Assert.Equal("match-not-found", await (await client.PostJsonAsync("/api/v1/me/matches/m99/apply", new { })).ProblemCodeAsync(HttpStatusCode.NotFound));

        var claimed = Guid.NewGuid();
        api.Learners.SetProfile(claimed, new LearnerCareerProfile(null, null, "Mumbai", "Maharashtra", null, [new("Customer service", "verified")]));
        await api.DeliverAsync("identity.claim.confirmed", new { claimId = Guid.NewGuid(), accountId = claimed, tenantId = TestTokens.Tenant });
        var claimedBoard = await (await api.ClientFor(claimed).GetAsync("/api/v1/me/matches")).JsonAsync(HttpStatusCode.OK);
        Assert.False(claimedBoard.GetProperty("visible").GetBoolean());
        Assert.True(claimedBoard.GetProperty("total").GetInt32() > 0);
    }

    [Fact]
    public async Task Erasure_removes_the_learner()
    {
        var me = Guid.NewGuid();
        var client = api.ClientFor(me);
        await client.PutJsonAsync("/api/v1/jobs/job/job-1/saved", new { saved = true });
        await client.PutJsonAsync("/api/v1/jobs/alerts", new { on = true, frequency = "daily", searches = Array.Empty<string>() });

        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("identity.learner.erased", new { accountId = me, tenantId = TestTokens.Tenant }));

        Assert.False(await api.QueryAsync(db => db.SavedJobs.AnyAsync(s => s.LearnerId == me)));
        Assert.False(await api.QueryAsync(db => db.Learners.AnyAsync(l => l.Id == me)));
    }
}
