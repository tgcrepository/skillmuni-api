using System.Net;
using Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Erasure;
using SkillMuni.Testing;

namespace Gamification.IntegrationTests;

/// <summary>
/// MISS-08: after <c>identity.learner.erased</c> only a tombstone (id + time) stays. The learner's still-valid token
/// gets 410 <c>learner-erased</c> instead of an empty, recreated profile, and a late event rebuilds nothing.
/// </summary>
[Collection(GamificationApiCollection.Name)]
public sealed class ErasureTests(GamificationApiFactory api)
{
    [Fact]
    public async Task After_erasure_requests_get_410_and_late_events_are_ignored()
    {
        var id = await api.JoinAsync("Asha", "Rao");
        var client = api.ClientFor(id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me/xp/ledger")).StatusCode);
        await api.DeliverAsync("identity.learner.erased", new { accountId = id, tenantId = TestTokens.Tenant });

        Assert.Equal("learner-erased", await (await client.GetAsync("/api/v1/me/xp/ledger")).ProblemCodeAsync(HttpStatusCode.Gone));
        Assert.Equal("learner-erased", await (await client.GetAsync("/api/v1/me/xp/ledger")).ProblemCodeAsync(HttpStatusCode.Gone));

        await api.DeliverAsync("learner.quest.step-completed", new { learnerId = id, slug = "who-dis", quest = 1, part = 1, paid = true, tenantId = TestTokens.Tenant });

        Assert.Equal("learner-erased", await (await client.GetAsync("/api/v1/me/xp/ledger")).ProblemCodeAsync(HttpStatusCode.Gone));
        Assert.False(await QueryAsync(db => db.Players.AnyAsync(p => p.Id == id)));
        Assert.True(await QueryAsync(db => db.Set<ErasedLearner>().AnyAsync(e => e.LearnerId == id)));
    }

    private async Task<T> QueryAsync<T>(Func<GamificationDbContext, Task<T>> query)
    {
        await using var scope = api.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        return await query(scope.ServiceProvider.GetRequiredService<GamificationDbContext>());
    }
}
