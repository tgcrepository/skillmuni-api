using Identity.Domain.Accounts;
using Identity.Domain.Claims;
using Identity.Domain.Codes;
using Identity.Domain.Consents;
using Identity.Domain.Registration;
using Identity.Domain.Sessions;
using SkillMuni.Common.Paging;

namespace Identity.Application.Abstractions;

/// <summary>Every query is scoped to the current tenant by the DbContext.</summary>
public interface IAccountRepository
{
    Task<Account?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<Account?> FindByPhoneAsync(string phone, CancellationToken cancellationToken);

    Task<Account?> FindByGoogleSubjectAsync(string subject, CancellationToken cancellationToken);

    Task<bool> EmailInUseAsync(string email, Guid? exceptAccountId, CancellationToken cancellationToken);

    Task<bool> PhoneInUseAsync(string phone, Guid? exceptAccountId, CancellationToken cancellationToken);

    void Add(Account account);
}

public interface IVerificationCodeRepository
{
    Task<VerificationCode?> FindLatestAsync(CodePurpose purpose, string target, CancellationToken cancellationToken);

    Task<VerificationCode?> FindByHashAsync(CodePurpose purpose, string codeHash, CancellationToken cancellationToken);

    Task<int> CountIssuedSinceAsync(CodePurpose purpose, string target, DateTime since, CancellationToken cancellationToken);

    void Add(VerificationCode code);
}

public interface ISessionRepository
{
    Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The session whose current or just-rotated refresh token has this hash.</summary>
    Task<Session?> FindByRefreshHashAsync(string refreshTokenHash, CancellationToken cancellationToken);

    /// <summary>Not revoked, not past its limit and seen after <paramref name="seenAfter"/>; newest activity first.</summary>
    Task<PagedResult<Session>> ListActiveAsync(Guid accountId, DateTime now, DateTime seenAfter, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Every session not yet revoked (for sign-out-everywhere).</summary>
    Task<IReadOnlyList<Session>> ListOpenAsync(Guid accountId, CancellationToken cancellationToken);

    void Add(Session session);
}

public interface IConsentRepository
{
    Task<PagedResult<ConsentRecord>> ListAsync(Guid accountId, PageRequest page, CancellationToken cancellationToken);

    void Add(ConsentRecord record);
}

public interface IPendingRegistrationRepository
{
    Task<PendingRegistration?> FindAsync(string email, CancellationToken cancellationToken);

    void Add(PendingRegistration registration);

    void Remove(PendingRegistration registration);
}

public interface IClaimInviteRepository
{
    Task<ClaimInvite?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    void Add(ClaimInvite invite);
}

/// <summary>Saves every change made in the use case, outbox rows included, in one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
