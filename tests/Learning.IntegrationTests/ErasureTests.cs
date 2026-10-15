using System.Net;
using Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Erasure;
using SkillMuni.Testing;

namespace Learning.IntegrationTests;

/// <summary>
/// MISS-08: after <c>identity.learner.erased</c> only a tombstone (id + time) stays. The learner's still-valid token
/// gets 410 <c>learner-erased</c> instead of an empty, recreated profile, and a late event rebuilds nothing.
/// </summary>
[Collection(LearningApiCollection.Name)]
public sealed class ErasureTests(LearningApiFactory api)
{
    [Fact]
    public async Task After_erasure_requests_get_410_and_late_events_are_ignored()
    {
        var client = api.ClientFor(out var id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/learn/modules/brain-vita")).StatusCode);
        await api.DeliverAsync("identity.learner.erased", new { accountId = id, tenantId = TestTokens.Tenant });

        Assert.Equal("learner-erased", await (await client.GetAsync("/api/v1/learn/modules/brain-vita")).ProblemCodeAsync(HttpStatusCode.Gone));
        Assert.Equal("learner-erased", await (await client.GetAsync("/api/v1/learn/modules/brain-vita")).ProblemCodeAsync(HttpStatusCode.Gone));

        await api.DeliverAsync("assessment.attempt.submitted", new { learnerId = id, tenantId = TestTokens.Tenant });

        Assert.Equal("learner-erased", await (await client.GetAsync("/api/v1/learn/modules/brain-vita")).ProblemCodeAsync(HttpStatusCode.Gone));
        Assert.False(await QueryAsync(db => db.LobbyStates.AnyAsync(x => x.LearnerId == id)));
        Assert.True(await QueryAsync(db => db.Set<ErasedLearner>().AnyAsync(e => e.LearnerId == id)));
    }

    private async Task<T> QueryAsync<T>(Func<LearningDbContext, Task<T>> query)
    {
        await using var scope = api.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        return await query(scope.ServiceProvider.GetRequiredService<LearningDbContext>());
    }
}
