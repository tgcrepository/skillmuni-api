using Microsoft.EntityFrameworkCore;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;

namespace Learner.IntegrationTests;

/// <summary>Two <c>cv.parse.completed</c> events handled at once must not both insert the job-profile row.</summary>
[Collection(LearnerApiCollection.Name)]
public sealed class JobProfileConcurrencyTests(LearnerApiFactory api)
{
    [Fact]
    public async Task Parallel_cv_parse_results_create_one_job_profile_row()
    {
        api.CreateLearnerClient(out var id);
        object Parsed() => new { cvId = Guid.NewGuid(), learnerId = id, outcome = "parsed", prefill = new Dictionary<string, object>(), tenantId = TestTokens.Tenant };

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => api.DeliverAsync("cv.parse.completed", Guid.NewGuid(), Parsed())));

        Assert.All(outcomes, o => Assert.Equal(DispatchOutcome.Handled, o));
        Assert.Equal(1, await api.QueryAsync(db => db.JobProfiles.CountAsync(j => j.LearnerId == id)));
    }
}
