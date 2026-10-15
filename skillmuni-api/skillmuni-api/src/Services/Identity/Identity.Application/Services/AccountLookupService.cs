using Identity.Application.Abstractions;
using Identity.Application.Errors;
using Identity.Domain.Common;
using SkillMuni.Common.Results;

namespace Identity.Application.Services;

public sealed record EmailOwnerDto(Guid AccountId);

/// <summary>Service-to-service lookups (internal ingress only), e.g. the CV parser's "email already used" check (S9).</summary>
public interface IAccountLookupService
{
    Task<Result<EmailOwnerDto>> FindEmailOwnerAsync(string email, CancellationToken cancellationToken);
}

public sealed class AccountLookupService(IAccountRepository accounts) : IAccountLookupService
{
    public async Task<Result<EmailOwnerDto>> FindEmailOwnerAsync(string email, CancellationToken cancellationToken)
    {
        if (EmailAddress.Normalize(email) is not { } normalized)
        {
            return IdentityErrors.InvalidEmail;
        }

        var account = await accounts.FindByEmailAsync(normalized, cancellationToken);
        return account is null ? IdentityErrors.AccountNotFound : new EmailOwnerDto(account.Id);
    }
}
