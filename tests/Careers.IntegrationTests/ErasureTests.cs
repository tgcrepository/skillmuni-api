using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Erasure;
using SkillMuni.Testing;
using Careers.Infrastructure.Persistence;

namespace Careers.IntegrationTests;

/// <summary>
/// MISS-08: after <c>identity.learner.erased</c> only a tombstone (id + time) stays. The learner's still-valid token
/// gets 410 <c>learner-erased</c> instead of an empty, recreated profile, and a late event rebuilds nothing.
/// </summary>
[Collection(CareersApiCollection.Name)]
public sealed class ErasureTests(CareersApiFactory api)
{
    [Fact]
    public async Task After_erasure_requests_get_410_and_late_events_are_ignored()
    {
        var id = Guid.NewGuid();
        var client = api.ClientFor(id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/fests/summary")).StatusCode);
        await api.DeliverAsync("identity.learner.erased", new { accountId = id, tenantId = TestTokens.Tenant });

        Assert.Equal("learner-erased", await (await client.GetAsync("/api/v1/fests/summary")).ProblemCodeAsync(HttpStatusCode.Gone));
        Assert.Equal("learner-erased", await (await client.GetAsync("/api/v1/fests/summary")).ProblemCodeAsync(HttpStatusCode.Gone));

        await api.DeliverAsync("learner.job-profile.completed", new { learnerId = id, tenantId = TestTokens.Tenant });

        Assert.Equal("learner-erased", await (await client.GetAsync("/api/v1/fests/summary")).ProblemCodeAsync(HttpStatusCode.Gone));
        Assert.False(await QueryAsync(db => db.Learners.AnyAsync(l => l.Id == id)));
        Assert.True(await QueryAsync(db => db.Set<ErasedLearner>().AnyAsync(e => e.LearnerId == id)));
    }

    private async Task<T> QueryAsync<T>(Func<CareersDbContext, Task<T>> query)
    {
        await using var scope = api.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        return await query(scope.ServiceProvider.GetRequiredService<CareersDbContext>());
    }
}
