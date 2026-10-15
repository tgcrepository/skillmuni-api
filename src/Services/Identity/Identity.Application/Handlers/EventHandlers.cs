using Identity.Application.Abstractions;
using Identity.Application.Events;
using Microsoft.Extensions.Logging;
using SkillMuni.Common.Messaging;

namespace Identity.Application.Handlers;

// Handlers change tracked entities only; the dispatcher saves them with the inbox row, so each event applies once.

/// <summary>A-277: keeps the Learner photo URL on the account for <c>GET session</c> and sign-in responses.</summary>
public sealed partial class PhotoChangedHandler(IAccountRepository accounts, TimeProvider clock, ILogger<PhotoChangedHandler> logger)
    : IIntegrationEventHandler<PhotoChanged>
{
    public async Task HandleAsync(PhotoChanged e, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(e.LearnerId, cancellationToken);
        if (account is null || account.IsErased)
        {
            LogSkipped(logger, e.LearnerId);
            return;
        }

        account.SetPhoto(e.PhotoUrl, clock.GetUtcNow().UtcDateTime);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Photo change for an unknown or erased account {AccountId}")]
    private static partial void LogSkipped(ILogger logger, Guid accountId);
}
