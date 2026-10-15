using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Application.Errors;
using Identity.Application.Events;
using Identity.Domain.Common;
using Identity.Domain.Consents;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Paging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Identity.Application.Services;

/// <summary>MISS-07: record, list (for audit) and withdraw consents. Records are append-only.</summary>
public interface IConsentService
{
    Task<Result<ConsentDto>> RecordAsync(Guid accountId, RecordConsentRequest request, CancellationToken cancellationToken);

    Task<PagedResult<ConsentDto>> ListAsync(Guid accountId, PageRequest page, CancellationToken cancellationToken);

    Task<Result> WithdrawAsync(Guid accountId, string scope, CancellationToken cancellationToken);
}

public sealed class ConsentService(
    IConsentRepository consents,
    IAccountRepository accounts,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<IdentityServiceOptions> options) : IConsentService
{
    private const string Source = "settings";

    public async Task<Result<ConsentDto>> RecordAsync(Guid accountId, RecordConsentRequest request, CancellationToken cancellationToken)
    {
        var record = await AddAsync(accountId, request.Scope, request.PolicyVersion, request.Granted, cancellationToken);
        return record is null ? IdentityErrors.NoSession : ToDto(record);
    }

    public async Task<PagedResult<ConsentDto>> ListAsync(Guid accountId, PageRequest page, CancellationToken cancellationToken) =>
        (await consents.ListAsync(accountId, page, cancellationToken)).Map(ToDto);

    public async Task<Result> WithdrawAsync(Guid accountId, string scope, CancellationToken cancellationToken)
    {
        var record = await AddAsync(accountId, scope, options.Value.PrivacyPolicyVersion, granted: false, cancellationToken);
        return record is null ? IdentityErrors.NoSession : Result.Success();
    }

    private async Task<ConsentRecord?> AddAsync(Guid accountId, string scope, string policyVersion, bool granted, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(accountId, cancellationToken);
        if (account is null || account.IsErased)
        {
            return null;
        }

        var record = ConsentRecord.Record(
            tenant.TenantId,
            accountId,
            scope,
            policyVersion,
            Market.For(account.Market).PrivacyLaw,
            granted,
            Source,
            clock.GetUtcNow().UtcDateTime);
        consents.Add(record);
        outbox.Add(new ConsentChanged(accountId, scope, policyVersion, granted) { TenantId = tenant.TenantId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return record;
    }

    private static ConsentDto ToDto(ConsentRecord r) =>
        new(r.Scope, r.PolicyVersion, r.Law, r.Granted, r.Source, r.RecordedAt.AsUtc());
}
