using Microsoft.Extensions.Options;
using Muni.Domain;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Tenancy;

namespace Muni.Application.Handlers;

// Each handler updates Muni's copy of the learner; the dispatcher saves it with the inbox row (idempotent).

/// <summary>Gets or creates the learner's copy, tracked.</summary>
public sealed class MuniLearnerAccessor(IMuniLearnerRepository learners, ITenantContext tenant, TimeProvider clock, IOptions<MuniOptions> options)
{
    public async Task<MuniLearner> GetOrCreateAsync(Guid learnerId, CancellationToken cancellationToken)
    {
        if (await learners.GetAsync(learnerId, cancellationToken) is { } known)
        {
            return known;
        }

        var ladder = options.Value.Ladder();
        var created = MuniLearner.Create(
            tenant.TenantId,
            learnerId,
            ladder[0].Level,
            ladder[0].Rank,
            ladder.Count > 1 ? ladder[1].Min : 0,
            clock.GetUtcNow().UtcDateTime);
        learners.Add(created);
        return created;
    }
}

public sealed class LearnerRegisteredHandler(MuniLearnerAccessor accessor, TimeProvider clock) : IIntegrationEventHandler<LearnerRegistered>
{
    public async Task HandleAsync(LearnerRegistered e, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(e.AccountId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        learner.Register(e.FirstName, e.LastName, e.OccurredAt == default ? now : e.OccurredAt, now);
    }
}

public sealed class XpAwardedHandler(MuniLearnerAccessor accessor, TimeProvider clock) : IIntegrationEventHandler<XpAwarded>
{
    public async Task HandleAsync(XpAwarded e, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(e.LearnerId, cancellationToken);
        learner.ApplyTotals(e.TotalXp, e.Level, e.Rank, e.NextLevelXp, e.StreakDays, clock.GetUtcNow().UtcDateTime);
    }
}

public sealed class QuestCompletedHandler(MuniLearnerAccessor accessor, TimeProvider clock) : IIntegrationEventHandler<QuestCompleted>
{
    public async Task HandleAsync(QuestCompleted e, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(e.LearnerId, cancellationToken);
        learner.CompleteQuest(e.Quest, clock.GetUtcNow().UtcDateTime);
    }
}

public sealed class AssessmentSubmittedHandler(MuniLearnerAccessor accessor, TimeProvider clock) : IIntegrationEventHandler<AssessmentSubmitted>
{
    public async Task HandleAsync(AssessmentSubmitted e, CancellationToken cancellationToken)
    {
        var learner = await accessor.GetOrCreateAsync(e.LearnerId, cancellationToken);
        learner.SetArchetype(e.ArchetypeName, clock.GetUtcNow().UtcDateTime);
    }
}

public sealed class LearnerErasedHandler(IMuniDataEraser eraser) : IIntegrationEventHandler<LearnerErased>
{
    public Task HandleAsync(LearnerErased e, CancellationToken cancellationToken) => eraser.EraseAsync(e.AccountId, cancellationToken);
}
