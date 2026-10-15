using Learner.Application.Abstractions;
using Learner.Application.Contracts;
using Learner.Domain;
using SkillMuni.Common.Tenancy;

namespace Learner.Application.Services;

/// <summary>
/// Loads the signed-in learner. The profile is normally created from <c>identity.learner.registered</c>; if a request
/// arrives before that event, it is created here from the token's claims and the event fills in the rest.
/// </summary>
public interface ILearnerAccessor
{
    Task<LearnerProfile> GetOrCreateAsync(LearnerRef who, CancellationToken cancellationToken);
}

public sealed class LearnerAccessor(ILearnerRepository learners, ITenantContext tenant, TimeProvider clock) : ILearnerAccessor
{
    public async Task<LearnerProfile> GetOrCreateAsync(LearnerRef who, CancellationToken cancellationToken)
    {
        var learner = await learners.GetAsync(who.Id, cancellationToken);
        if (learner is not null)
        {
            return learner;
        }

        var candidate = LearnerProfile.Create(who.Id, tenant.TenantId, who.Market, who.FirstName, string.Empty, clock.GetUtcNow().UtcDateTime);
        return await learners.GetOrAddAsync(candidate, cancellationToken);
    }
}
