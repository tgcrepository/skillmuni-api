using Identity.Application.Abstractions;
using Identity.Domain.Accounts;
using Identity.Domain.Claims;
using Identity.Domain.Codes;
using Identity.Domain.Consents;
using Identity.Domain.Registration;
using Identity.Domain.Sessions;
using Microsoft.EntityFrameworkCore;
using SkillMuni.Common.Paging;

namespace Identity.Infrastructure.Persistence;

// Repositories return tracked entities when the caller will change them and use AsNoTracking for pure reads.
// LINQ only: EF parameterises every value (CLAUDE.md §5).

internal sealed class AccountRepository(IdentityDbContext db) : IAccountRepository
{
    public Task<Account?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Accounts.FirstOrDefaultAsync(a => a.Email == email && a.Status != AccountStatus.Erased, cancellationToken);

    public Task<Account?> FindByPhoneAsync(string phone, CancellationToken cancellationToken) =>
        db.Accounts.FirstOrDefaultAsync(a => a.Phone == phone && a.Status != AccountStatus.Erased, cancellationToken);

    public Task<Account?> FindByGoogleSubjectAsync(string subject, CancellationToken cancellationToken) =>
        db.Accounts.FirstOrDefaultAsync(a => a.GoogleSubject == subject && a.Status != AccountStatus.Erased, cancellationToken);

    public Task<bool> EmailInUseAsync(string email, Guid? exceptAccountId, CancellationToken cancellationToken) =>
        db.Accounts.AsNoTracking().AnyAsync(a => a.Email == email && (exceptAccountId == null || a.Id != exceptAccountId), cancellationToken);

    public Task<bool> PhoneInUseAsync(string phone, Guid? exceptAccountId, CancellationToken cancellationToken) =>
        db.Accounts.AsNoTracking().AnyAsync(a => a.Phone == phone && (exceptAccountId == null || a.Id != exceptAccountId), cancellationToken);

    public void Add(Account account) => db.Accounts.Add(account);
}

internal sealed class VerificationCodeRepository(IdentityDbContext db) : IVerificationCodeRepository
{
    public Task<VerificationCode?> FindLatestAsync(CodePurpose purpose, string target, CancellationToken cancellationToken) =>
        db.VerificationCodes
            .Where(c => c.Purpose == purpose && c.Target == target)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<VerificationCode?> FindByHashAsync(CodePurpose purpose, string codeHash, CancellationToken cancellationToken) =>
        db.VerificationCodes.FirstOrDefaultAsync(c => c.Purpose == purpose && c.CodeHash == codeHash, cancellationToken);

    public Task<int> CountIssuedSinceAsync(CodePurpose purpose, string target, DateTime since, CancellationToken cancellationToken) =>
        db.VerificationCodes.AsNoTracking().CountAsync(c => c.Purpose == purpose && c.Target == target && c.CreatedAt >= since, cancellationToken);

    public void Add(VerificationCode code) => db.VerificationCodes.Add(code);
}

internal sealed class SessionRepository(IdentityDbContext db) : ISessionRepository
{
    public Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Sessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Session?> FindByRefreshHashAsync(string refreshTokenHash, CancellationToken cancellationToken) =>
        db.Sessions.FirstOrDefaultAsync(
            s => s.RefreshTokenHash == refreshTokenHash || s.PreviousRefreshTokenHash == refreshTokenHash,
            cancellationToken);

    public async Task<PagedResult<Session>> ListActiveAsync(
        Guid accountId,
        DateTime now,
        DateTime seenAfter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var query = db.Sessions.AsNoTracking()
            .Where(s => s.AccountId == accountId && s.RevokedAt == null && s.ExpiresAt > now && s.LastSeenAt > seenAfter);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(s => s.LastSeenAt)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Session>(items, page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyList<Session>> ListOpenAsync(Guid accountId, CancellationToken cancellationToken) =>
        await db.Sessions.Where(s => s.AccountId == accountId && s.RevokedAt == null).ToListAsync(cancellationToken);

    public void Add(Session session) => db.Sessions.Add(session);
}

internal sealed class ConsentRepository(IdentityDbContext db) : IConsentRepository
{
    public async Task<PagedResult<ConsentRecord>> ListAsync(Guid accountId, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Consents.AsNoTracking().Where(c => c.AccountId == accountId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(c => c.RecordedAt)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<ConsentRecord>(items, page.Page, page.PageSize, total);
    }

    public void Add(ConsentRecord record) => db.Consents.Add(record);
}

internal sealed class PendingRegistrationRepository(IdentityDbContext db) : IPendingRegistrationRepository
{
    public Task<PendingRegistration?> FindAsync(string email, CancellationToken cancellationToken) =>
        db.PendingRegistrations.FirstOrDefaultAsync(p => p.Email == email, cancellationToken);

    public void Add(PendingRegistration registration) => db.PendingRegistrations.Add(registration);

    public void Remove(PendingRegistration registration) => db.PendingRegistrations.Remove(registration);
}

internal sealed class ClaimInviteRepository(IdentityDbContext db) : IClaimInviteRepository
{
    public Task<ClaimInvite?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.ClaimInvites.FirstOrDefaultAsync(c => c.TokenHash == tokenHash, cancellationToken);

    public void Add(ClaimInvite invite) => db.ClaimInvites.Add(invite);
}
