using System.Net;
using SkillMuni.Testing;

namespace Gamification.IntegrationTests;

/// <summary>The Arena GET stores the rank the learner saw; that write must never fail the read on a busy player.</summary>
[Collection(GamificationApiCollection.Name)]
public sealed class ArenaConcurrencyTests(GamificationApiFactory api)
{
    [Fact]
    public async Task Arena_reads_stay_200_while_events_update_the_same_player()
    {
        var me = await api.JoinAsync("Busy", "Bee");
        var client = api.ClientFor(me);
        var parts = Enumerable.Range(1, 6).Select(part => (Task)api.DeliverAsync(
            "learner.quest.step-completed",
            new { learnerId = me, slug = "who-dis", quest = 1, part, paid = true, tenantId = TestTokens.Tenant }));
        var reads = Enumerable.Range(0, 12).Select(_ => client.GetAsync("/api/v1/arena?scope=everyone&period=week")).ToList();

        await Task.WhenAll(parts.Concat(reads));

        foreach (var read in reads)
        {
            Assert.Equal(HttpStatusCode.OK, (await read).StatusCode);
        }
    }
}
