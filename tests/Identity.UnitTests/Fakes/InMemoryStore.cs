using Identity.Application.Abstractions;
using Identity.Domain.Accounts;
using Identity.Domain.Claims;
using Identity.Domain.Codes;
using Identity.Domain.Consents;
using Identity.Domain.Registration;
using Identity.Domain.Sessions;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Paging;
using SkillMuni.Common.Tenancy;

namespace Identity.UnitTests.Fakes;

/// <summary>In-memory stand-ins for the repositories, so the Application layer runs without a database.</summary>
public sealed class InMemoryStore :
    IAccountRepository,
    IVerificationCodeRepository,
    ISessionRepository,
    IConsentRepository,
    IPendingRegistrationRepository,
    IClaimInviteRepository,
    IUnitOfWork,
    IOutbox
{
    public List<Account> Accounts { get; } = [];

    public List<VerificationCode> Codes { get; } = [];

    public List<Session> Sessions { get; } = [];

    public List<ConsentRecord> Consents { get; } = [];

    public List<PendingRegistration> Pending { get; } = [];

    public List<ClaimInvite> Claims { get; } = [];

    public List<IntegrationEvent> Events { get; } = [];

    public int Saves { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public void Add(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);

    // Accounts
    public Task<Account?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Id == id));

    public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Email == email && !a.IsErased));

    public Task<Account?> FindByPhoneAsync(string phone, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Phone == phone && !a.IsErased));

    public Task<Account?> FindByGoogleSubjectAsync(string subject, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.GoogleSubject == subject && !a.IsErased));

    public Task<bool> EmailInUseAsync(string email, Guid? exceptAccountId, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.Any(a => a.Email == email && a.Id != exceptAccountId));

    public Task<bool> PhoneInUseAsync(string phone, Guid? exceptAccountId, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.Any(a => a.Phone == phone && a.Id != exceptAccountId));

    public void Add(Account account) => Accounts.Add(account);

    // Codes
    public Task<VerificationCode?> FindLatestAsync(CodePurpose purpose, string target, CancellationToken cancellationToken) =>
        Task.FromResult(Codes.Where(c => c.Purpose == purpose && c.Target == target).OrderByDescending(c => c.CreatedAt).FirstOrDefault());

    public Task<VerificationCode?> FindByHashAsync(CodePurpose purpose, string codeHash, CancellationToken cancellationToken) =>
        Task.FromResult(Codes.FirstOrDefault(c => c.Purpose == purpose && c.CodeHash == codeHash));

    public Task<int> CountIssuedSinceAsync(CodePurpose purpose, string target, DateTime since, CancellationToken cancellationToken) =>
        Task.FromResult(Codes.Count(c => c.Purpose == purpose && c.Target == target && c.CreatedAt >= since));

    public void Add(VerificationCode code) => Codes.Add(code);

    // Sessions
    Task<Session?> ISessionRepository.GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.Id == id));

    public Task<Session?> FindByRefreshHashAsync(string refreshTokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.RefreshTokenHash == refreshTokenHash || s.PreviousRefreshTokenHash == refreshTokenHash));

    public Task<PagedResult<Session>> ListActiveAsync(Guid accountId, DateTime now, DateTime seenAfter, PageRequest page, CancellationToken cancellationToken)
    {
        var all = Sessions.Where(s => s.AccountId == accountId && s.RevokedAt == null && s.ExpiresAt > now && s.LastSeenAt > seenAfter).ToList();
        return Task.FromResult(new PagedResult<Session>(all.Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, all.Count));
    }

    public Task<IReadOnlyList<Session>> ListOpenAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Session>>(Sessions.Where(s => s.AccountId == accountId && s.RevokedAt == null).ToList());

    public void Add(Session session) => Sessions.Add(session);

    // Consents
    public Task<PagedResult<ConsentRecord>> ListAsync(Guid accountId, PageRequest page, CancellationToken cancellationToken)
    {
        var all = Consents.Where(c => c.AccountId == accountId).ToList();
        return Task.FromResult(new PagedResult<ConsentRecord>(all.Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, all.Count));
    }

    public void Add(ConsentRecord record) => Consents.Add(record);

    // Pending registrations
    public Task<PendingRegistration?> FindAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Pending.FirstOrDefault(p => p.Email == email));

    public void Add(PendingRegistration registration) => Pending.Add(registration);

    public void Remove(PendingRegistration registration) => Pending.Remove(registration);

    // Claims
    public Task<ClaimInvite?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Claims.FirstOrDefault(c => c.TokenHash == tokenHash));

    public void Add(ClaimInvite invite) => Claims.Add(invite);
}

public sealed class FixedTenant(string market = "IN") : ITenantContext
{
    public string TenantId => "skillmuni-in";

    public string Market => market;
}
