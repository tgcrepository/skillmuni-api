using System.Net;
using Microsoft.EntityFrameworkCore;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;

namespace Learning.IntegrationTests;

/// <summary>The Home lobby request and <c>assessment.attempt.submitted</c> creating the lobby row at once: one row, no errors.</summary>
[Collection(LearningApiCollection.Name)]
public sealed class LobbyConcurrencyTests(LearningApiFactory api)
{
    [Fact]
    public async Task Parallel_home_requests_and_assessment_events_create_one_lobby_row()
    {
        var client = api.ClientFor(out var id);
        var events = Enumerable.Range(0, 3).Select(_ => api.DeliverAsync("assessment.attempt.submitted", new { learnerId = id, tenantId = TestTokens.Tenant })).ToList();
        var reads = Enumerable.Range(0, 3).Select(_ => client.GetAsync("/api/v1/home")).ToList();

        await Task.WhenAll(events.Cast<Task>().Concat(reads));

        foreach (var e in events)
        {
            Assert.Equal(DispatchOutcome.Handled, await e);
        }

        foreach (var r in reads)
        {
            Assert.Equal(HttpStatusCode.OK, (await r).StatusCode);
        }

        Assert.Equal(1, await api.QueryAsync(db => db.LobbyStates.CountAsync(s => s.LearnerId == id)));
    }
}
