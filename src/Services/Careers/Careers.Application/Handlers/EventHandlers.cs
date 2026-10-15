using Careers.Application.Abstractions;
using Careers.Application.Services;
using Microsoft.Extensions.Logging;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Storage;

namespace Careers.Application.Handlers;

// One handler per consumed event. Each updates the learner's projection; the dispatcher saves it with the inbox row.

/// <summary>Launchpad "Take assessment" mission and assessment card; matching's assessment factor.</summary>
public sealed class AssessmentSubmittedHandler(CareerStateLoader states, CareerClock clock) : IIntegrationEventHandler<AssessmentSubmitted>
{
    public async Task HandleAsync(AssessmentSubmitted e, CancellationToken cancellationToken)
    {
        var learner = await states.LearnerForUpdateAsync(e.LearnerId, cancellationToken);
        var now = clock.UtcNow;
        var takenAt = e.OccurredAt == default ? now : DateTime.SpecifyKind(e.OccurredAt, DateTimeKind.Utc);
        learner.AssessmentSubmitted(e.Attempt, e.ArchetypeName, e.ArchetypeSummary, e.TopDrivers ?? [], takenAt, now);
    }
}

/// <summary>Launchpad "Finish job profile" mission (BR-110).</summary>
public sealed class JobProfileCompletedHandler(CareerStateLoader states, CareerClock clock) : IIntegrationEventHandler<JobProfileCompleted>
{
    public async Task HandleAsync(JobProfileCompleted e, CancellationToken cancellationToken)
    {
        var learner = await states.LearnerForUpdateAsync(e.LearnerId, cancellationToken);
        var now = clock.UtcNow;
        learner.JobProfileCompleted(e.OccurredAt == default ? now : DateTime.SpecifyKind(e.OccurredAt, DateTimeKind.Utc), now);
    }
}

/// <summary>BR-72: a confirmed institute claim shows matches before the profile is published.</summary>
public sealed class ClaimConfirmedHandler(CareerStateLoader states, CareerClock clock) : IIntegrationEventHandler<ClaimConfirmed>
{
    public async Task HandleAsync(ClaimConfirmed e, CancellationToken cancellationToken)
    {
        var learner = await states.LearnerForUpdateAsync(e.AccountId, cancellationToken);
        var now = clock.UtcNow;
        learner.ClaimConfirmed(e.OccurredAt == default ? now : DateTime.SpecifyKind(e.OccurredAt, DateTimeKind.Utc), now);
    }
}

/// <summary>MISS-08: every row and CV file this service holds for the learner. Files first, so a retry can still find them.</summary>
public sealed partial class AccountErasedHandler(
    ICvFileRepository cvFiles,
    IBlobStore blobs,
    ICareersDataEraser eraser,
    ILogger<AccountErasedHandler> logger) : IIntegrationEventHandler<AccountErased>
{
    public async Task HandleAsync(AccountErased e, CancellationToken cancellationToken)
    {
        var files = await cvFiles.ListAsync(e.AccountId, cancellationToken);
        foreach (var file in files)
        {
            await blobs.DeleteAsync(file.BlobName, cancellationToken);
        }

        await eraser.EraseAsync(e.AccountId, cancellationToken);
        LogErased(logger, e.AccountId, files.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Careers data erased {LearnerId} {Files}")]
    private static partial void LogErased(ILogger logger, Guid learnerId, int files);
}
