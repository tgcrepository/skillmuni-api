using Assessment.Domain;
using Microsoft.Extensions.Logging;
using SkillMuni.Common.Messaging;

namespace Assessment.Application.Handlers;

// The dispatcher sets the tenant, then saves the handler's changes together with the inbox row (idempotent on eventId).

/// <summary>Starts the learner's journey row (requests also create it lazily if they arrive first).</summary>
public sealed class AccountRegisteredHandler(ICareerJourneyRepository journeys, TimeProvider clock) : IIntegrationEventHandler<AccountRegistered>
{
    public async Task HandleAsync(AccountRegistered e, CancellationToken cancellationToken)
    {
        if (!await journeys.ExistsAsync(e.AccountId, cancellationToken))
        {
            journeys.Add(CareerJourney.Start(e.AccountId, e.TenantId, clock.GetUtcNow().UtcDateTime));
        }
    }
}

/// <summary>MISS-08: the learner's attempts and journey are deleted.</summary>
public sealed partial class AccountErasedHandler(IAssessmentDataEraser eraser, ILogger<AccountErasedHandler> logger) : IIntegrationEventHandler<AccountErased>
{
    public async Task HandleAsync(AccountErased e, CancellationToken cancellationToken)
    {
        await eraser.EraseAsync(e.AccountId, cancellationToken);
        LogErased(logger, e.AccountId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Assessment data erased {LearnerId}")]
    private static partial void LogErased(ILogger logger, Guid learnerId);
}
