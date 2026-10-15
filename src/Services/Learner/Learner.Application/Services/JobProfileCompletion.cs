using Learner.Application.Abstractions;
using Learner.Application.Events;
using Learner.Domain;
using SkillMuni.Common.Messaging;

namespace Learner.Application.Services;

/// <summary>
/// BR-40 / BR-110: publishes <c>learner.job-profile.completed</c> the first time the Job Profile reaches 100%
/// (every one of the 18 quest steps filled; a skipped optional step counts as not filled, A-262).
/// </summary>
public interface IJobProfileCompletion
{
    /// <param name="changedSlug">A quest whose answers changed in this unit of work and aren't saved yet.</param>
    /// <param name="changedJson">That quest's answers as they will be saved.</param>
    Task CheckAsync(LearnerProfile learner, string? changedSlug, string? changedJson, CancellationToken cancellationToken);
}

public sealed class JobProfileCompletion(
    IQuestAnswersRepository answers,
    IProfileProgressService profileProgress,
    QuestCatalog catalog,
    IOutbox outbox,
    TimeProvider clock) : IJobProfileCompletion
{
    public async Task CheckAsync(LearnerProfile learner, string? changedSlug, string? changedJson, CancellationToken cancellationToken)
    {
        // 100% needs every quest done but the last (whose single step may be in progress), so most saves skip the query.
        if (learner.JobProfileCompletedAt is not null || learner.QuestsCompleted < catalog.Quests.Count - 1)
        {
            return;
        }

        var all = new Dictionary<string, string>(await answers.ListAsync(learner.Id, cancellationToken), StringComparer.Ordinal);
        if (changedSlug is not null && changedJson is not null)
        {
            all[changedSlug] = changedJson;
        }

        var progress = profileProgress.Calculate(learner, all);
        if (progress.Total > 0 && progress.Filled == progress.Total && learner.MarkJobProfileComplete(clock.GetUtcNow().UtcDateTime))
        {
            outbox.Add(new JobProfileCompleted(learner.Id) { TenantId = learner.TenantId });
        }
    }
}
