using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Application.Errors;
using Identity.Application.Events;
using Identity.Domain.Accounts;
using Identity.Domain.Common;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Identity.Application.Services;

/// <summary>
/// A-253: swap a Google ID token for a session. Existing Google link → sign in; verified email already on an
/// account → link and sign in; otherwise create the learner (<c>isNew</c> = true, the app opens the quests intro).
/// </summary>
public interface IGoogleSignInService
{
    Task<Result<SignedIn<GoogleSignInDto>>> SignInAsync(GoogleSignInRequest request, CancellationToken cancellationToken);
}

public sealed class GoogleSignInService(
    IGoogleTokenValidator google,
    ITenantSettings tenantSettings,
    IAccountRepository accounts,
    ISessionService sessions,
    IOutbox outbox,
    ITenantContext tenant,
    TimeProvider clock) : IGoogleSignInService
{
    public async Task<Result<SignedIn<GoogleSignInDto>>> SignInAsync(GoogleSignInRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantSettings.GoogleClientId))
        {
            return IdentityErrors.GoogleUnavailable;
        }

        var identity = await google.ValidateAsync(request.Credential, tenantSettings.GoogleClientId, cancellationToken);
        var email = EmailAddress.Normalize(identity?.Email);
        if (identity is null || email is null || !identity.EmailVerified)
        {
            return IdentityErrors.GoogleInvalid;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var isNew = false;
        var account = await accounts.FindByGoogleSubjectAsync(identity.Subject, cancellationToken);
        if (account is null)
        {
            account = await accounts.FindByEmailAsync(email, cancellationToken);
            if (account is not null)
            {
                account.LinkGoogle(identity.Subject, now);
            }
            else
            {
                var market = Market.For(tenant.Market);
                var firstName = string.IsNullOrWhiteSpace(identity.GivenName) ? email.Split('@')[0] : identity.GivenName;
                account = Account.RegisterWithGoogle(tenant.TenantId, market.Code, firstName, identity.FamilyName ?? string.Empty, email, identity.Subject, now);
                accounts.Add(account);
                outbox.Add(new LearnerRegistered(account.Id, account.FirstName, account.LastName, email, null, market.Code, "google")
                {
                    TenantId = tenant.TenantId,
                });
                isNew = true;
            }
        }

        if (account.IsLocked(now))
        {
            return IdentityErrors.AccountLocked;
        }

        var signedIn = await sessions.SignInAsync(account, cancellationToken);
        return new SignedIn<GoogleSignInDto>(new GoogleSignInDto(signedIn.Body, isNew), signedIn.Tokens);
    }
}
