using System.Net;
using Learner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Tenancy;
using SkillMuni.Messaging.Erasure;
using SkillMuni.Testing;

namespace Learner.IntegrationTests;

/// <summary>
/// MISS-08: after <c>identity.learner.erased</c> only a tombstone (id + time) stays. The learner's still-valid token
/// gets 410 <c>learner-erased</c> instead of an empty, recreated profile, and a late event rebuilds nothing.
/// </summary>
[Collection(LearnerApiCollection.Name)]
public sealed class ErasureTests(LearnerApiFactory api)
{
    [Fact]
    public async Task After_erasure_requests_get_410_and_late_events_are_ignored()
    {
        var client = api.CreateLearnerClient(out var id);
        await api.DeliverAsync("identity.learner.registered", Guid.NewGuid(), new { accountId = id, firstName = "Asha", lastName = "Rao", phone = "+919876540111", market = "IN", source = "phone", tenantId = TestTokens.Tenant });
        Assert.True(await QueryAsync(db => db.Learners.AnyAsync(l => l.Id == id)));
        await api.DeliverAsync("identity.learner.erased", Guid.NewGuid(), new { accountId = id, tenantId = TestTokens.Tenant });

        Assert.Equal("learner-erased", await (await client.GetAsync("/api/v1/me/progress")).ProblemCodeAsync(HttpStatusCode.Gone));
        Assert.Equal("learner-erased", await (await client.GetAsync("/api/v1/me/progress")).ProblemCodeAsync(HttpStatusCode.Gone));

        await api.DeliverAsync("gamification.xp.awarded", Guid.NewGuid(), new { learnerId = id, totalXp = 200, level = 1, rank = "Rookie", levelStartXp = 0, nextLevelXp = 300, badges = 0, streakDays = 0, tenantId = TestTokens.Tenant });

        Assert.Equal("learner-erased", await (await client.GetAsync("/api/v1/me/progress")).ProblemCodeAsync(HttpStatusCode.Gone));
        Assert.False(await QueryAsync(db => db.Learners.AnyAsync(l => l.Id == id)));
        Assert.True(await QueryAsync(db => db.Set<ErasedLearner>().AnyAsync(e => e.LearnerId == id)));
    }

    private async Task<T> QueryAsync<T>(Func<LearnerDbContext, Task<T>> query)
    {
        await using var scope = api.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantOverride>().Set(TestTokens.Tenant);
        return await query(scope.ServiceProvider.GetRequiredService<LearnerDbContext>());
    }
}
