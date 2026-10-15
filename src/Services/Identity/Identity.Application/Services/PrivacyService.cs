using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Application.Errors;
using Identity.Application.Events;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;

namespace Identity.Application.Services;

/// <summary>
/// MISS-08, DPDP / POPIA rights. Export and erasure fan out to every service by event; retention jobs wait on the
/// retention-policy decision (BRD §9).
/// </summary>
public interface IPrivacyService
{
    Task<Result<DataExportRequestedDto>> RequestExportAsync(Guid accountId, CancellationToken cancellationToken);

    Task<Result> EraseAsync(Guid accountId, CancellationToken cancellationToken);
}

public sealed class PrivacyService(
    IAccountRepository accounts,
    ISessionService sessions,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IPrivacyService
{
    public async Task<Result<DataExportRequestedDto>> RequestExportAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(accountId, cancellationToken);
        if (account is null || account.IsErased)
        {
            return IdentityErrors.NoSession;
        }

        outbox.Add(new DataExportRequested(accountId) { TenantId = account.TenantId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DataExportRequestedDto(clock.GetUtcNow().UtcDateTime);
    }

    public async Task<Result> EraseAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(accountId, cancellationToken);
        if (account is null || account.IsErased)
        {
            return IdentityErrors.NoSession;
        }

        account.Erase(clock.GetUtcNow().UtcDateTime);
        await sessions.RevokeAllAsync(accountId, cancellationToken);
        outbox.Add(new LearnerErased(accountId) { TenantId = account.TenantId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
