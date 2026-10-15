using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Application.Errors;
using Identity.Domain.Accounts;
using Identity.Domain.Sessions;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Paging;
using SkillMuni.Common.Results;

namespace Identity.Application.Services;

/// <summary>
/// Sessions (MISS-13): a 10-minute access JWT plus a rotating refresh token; signed out after 30 minutes idle or
/// 30 days in all. The gateway refreshes silently, so the frontend contract doesn't change.
/// </summary>
public interface ISessionService
{
    /// <summary>Starts a session and saves the unit of work (the caller's pending changes included).</summary>
    Task<SignedIn<AuthUserDto>> SignInAsync(Account account, CancellationToken cancellationToken);

    Task<Result<SessionTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task<Result<AuthUserDto>> CurrentAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Revokes the session from the access token, or from the refresh token when the access token has lapsed.</summary>
    Task LogoutAsync(Guid? sessionId, string? refreshToken, CancellationToken cancellationToken);

    Task<PagedResult<SessionInfoDto>> ListAsync(Guid accountId, Guid? currentSessionId, PageRequest page, CancellationToken cancellationToken);

    Task<Result> RevokeAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Revokes every open session (not saved; part of the caller's unit of work).</summary>
    Task RevokeAllAsync(Guid accountId, CancellationToken cancellationToken);
}

public sealed class SessionService(
    ISessionRepository sessions,
    IAccountRepository accounts,
    ITokenIssuer tokenIssuer,
    ICodeGenerator generator,
    ISecretHasher hasher,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IOptions<IdentityServiceOptions> options) : ISessionService
{
    private readonly IdentityServiceOptions _options = options.Value;

    public async Task<SignedIn<AuthUserDto>> SignInAsync(Account account, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var refreshToken = generator.NewToken();
        var session = Session.Start(account.TenantId, account.Id, hasher.Hash(refreshToken), now, _options.SessionMaxAge, currentUser.UserAgent);
        sessions.Add(session);
        account.RecordLogin(now);

        var access = tokenIssuer.Issue(account, session, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SignedIn<AuthUserDto>(
            account.ToAuthUser(),
            new SessionTokens(access.Value, access.ExpiresAt, refreshToken, session.ExpiresAt));
    }

    public async Task<Result<SessionTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = hasher.Hash(refreshToken);
        var session = await sessions.FindByRefreshHashAsync(hash, cancellationToken);
        if (session is null)
        {
            return IdentityErrors.NoSession;
        }

        var state = session.Evaluate(now, _options.IdleTimeout);
        if (state != SessionState.Active)
        {
            if (state == SessionState.Idle)
            {
                session.Revoke(now);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return IdentityErrors.SessionExpired;
        }

        var account = await accounts.GetAsync(session.AccountId, cancellationToken);
        if (account is null || account.IsErased || account.IsLocked(now))
        {
            session.Revoke(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return IdentityErrors.NoSession;
        }

        if (session.RefreshTokenHash != hash)
        {
            // A rotated-out token: fine within the grace window (parallel tabs); otherwise treat it as stolen.
            if (!session.IsInGrace(hash, now, _options.RefreshGrace))
            {
                session.Revoke(now);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return IdentityErrors.SessionExpired;
            }

            var graceAccess = tokenIssuer.Issue(account, session, now);
            return new SessionTokens(graceAccess.Value, graceAccess.ExpiresAt, null, session.ExpiresAt);
        }

        var newRefreshToken = generator.NewToken();
        session.Rotate(hasher.Hash(newRefreshToken), now);
        var access = tokenIssuer.Issue(account, session, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SessionTokens(access.Value, access.ExpiresAt, newRefreshToken, session.ExpiresAt);
    }

    public async Task<Result<AuthUserDto>> CurrentAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var session = await sessions.GetAsync(sessionId, cancellationToken);
        if (session is null || session.AccountId != accountId || session.Evaluate(now, _options.IdleTimeout) != SessionState.Active)
        {
            return IdentityErrors.NoSession;
        }

        var account = await accounts.GetAsync(accountId, cancellationToken);
        if (account is null || account.IsErased)
        {
            return IdentityErrors.NoSession;
        }

        session.Touch(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return account.ToAuthUser();
    }

    public async Task LogoutAsync(Guid? sessionId, string? refreshToken, CancellationToken cancellationToken)
    {
        var session = sessionId is { } id
            ? await sessions.GetAsync(id, cancellationToken)
            : string.IsNullOrEmpty(refreshToken) ? null : await sessions.FindByRefreshHashAsync(hasher.Hash(refreshToken), cancellationToken);
        if (session is not null)
        {
            session.Revoke(clock.GetUtcNow().UtcDateTime);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<PagedResult<SessionInfoDto>> ListAsync(Guid accountId, Guid? currentSessionId, PageRequest page, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var list = await sessions.ListActiveAsync(accountId, now, now - _options.IdleTimeout, page, cancellationToken);
        return list.Map(s => new SessionInfoDto(s.Id, s.CreatedAt.AsUtc(), s.LastSeenAt.AsUtc(), s.UserAgent, s.Id == currentSessionId));
    }

    public async Task<Result> RevokeAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(sessionId, cancellationToken);
        if (session is null || session.AccountId != accountId)
        {
            return IdentityErrors.SessionNotFound;
        }

        session.Revoke(clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task RevokeAllAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var session in await sessions.ListOpenAsync(accountId, cancellationToken))
        {
            session.Revoke(now);
        }
    }
}
