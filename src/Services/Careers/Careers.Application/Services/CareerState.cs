using Careers.Application.Abstractions;
using Careers.Domain;
using SkillMuni.Common.Tenancy;

namespace Careers.Application.Services;

/// <summary>The learner's career state for one request: their row, bookmarks, applications and match profile.</summary>
public sealed record CareerState(
    Guid LearnerId,
    CareerLearner? Learner,
    IReadOnlyDictionary<string, DateTime> Saved,
    IReadOnlyList<JobApplication> Applications,
    LearnerCareerProfile? Profile)
{
    /// <summary>Null while Learner can't tell us the learner's skills: matches are unknown, not 0 %.</summary>
    public MatchProfile? MatchProfile =>
        Profile is null ? null : new MatchProfile(Profile.Skills, Profile.City, Profile.State, Profile.Relocate, Learner is { AssessmentDone: true } l ? l.ArchetypeName : null);

    /// <summary>Applications sent from My Jobs and not withdrawn (the "Apply to a job" mission, BR-60).</summary>
    public int ActiveApplications => Applications.Count(a => a.Active && a.Source == ApplicationSource.Apply);
}

/// <summary>Loads <see cref="CareerState"/>, and the learner's row for a change (created on first contact).</summary>
public sealed class CareerStateLoader(
    ICareerLearnerRepository learners,
    ISavedJobRepository saved,
    IApplicationRepository applications,
    ILearnerDirectory directory,
    ITenantContext tenant,
    CareerClock clock)
{
    public async Task<CareerState> LoadAsync(Guid learnerId, bool withProfile, CancellationToken cancellationToken)
    {
        // Learner is called first; the DbContext reads below run one at a time.
        var profile = withProfile ? await directory.CareerProfileAsync(learnerId, cancellationToken) : null;
        var learner = await learners.GetAsync(learnerId, cancellationToken);
        var bookmarks = await saved.ListAsync(learnerId, cancellationToken);
        var sent = await applications.ListAsync(learnerId, cancellationToken);
        return new CareerState(learnerId, learner, bookmarks, sent, profile);
    }

    public async Task<CareerLearner> LearnerForUpdateAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        var learner = await learners.GetAsync(learnerId, cancellationToken);
        if (learner is null)
        {
            learner = CareerLearner.Start(learnerId, tenant.TenantId, clock.UtcNow);
            learners.Add(learner);
        }

        return learner;
    }
}
