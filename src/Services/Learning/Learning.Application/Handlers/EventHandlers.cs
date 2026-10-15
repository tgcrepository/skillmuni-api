using Learning.Domain;
using SkillMuni.Common.Messaging;

namespace Learning.Application.Handlers;

// The dispatcher applies the event's tenant and saves the handler's changes together with the inbox row.

/// <summary>MISS-08: erase every row Learning holds for the learner.</summary>
public sealed class AccountErasedHandler(ILearnerDataEraser eraser) : IIntegrationEventHandler<AccountErased>
{
    public Task HandleAsync(AccountErased e, CancellationToken cancellationToken) => eraser.EraseAsync(e.AccountId, cancellationToken);
}

/// <summary>BR-42: the first move "Discover your Career Drivers" is done once the assessment is submitted.</summary>
public sealed class AssessmentSubmittedHandler(ILobbyRepository lobby, TimeProvider clock) : IIntegrationEventHandler<AssessmentSubmitted>
{
    public async Task HandleAsync(AssessmentSubmitted e, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var state = await lobby.GetAsync(e.LearnerId, cancellationToken);
        if (state is null)
        {
            state = LobbyState.Start(e.TenantId, e.LearnerId, now);
            lobby.Add(state);
        }

        state.MarkAssessmentDone(now);
    }
}
