using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Application.Errors;
using Identity.Application.Events;
using Identity.Domain.Accounts;
using Identity.Domain.Codes;
using Identity.Domain.Common;
using Identity.Domain.Consents;
using Identity.Domain.Registration;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Identity.Application.Services;

/// <summary>"Join the squad": details + consent → email code → account, consent record and a session.</summary>
public interface IRegistrationService
{
    Task<Result<EmailChallengeDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<Result<EmailChallengeDto>> ResendAsync(SendEmailCodeRequest request, CancellationToken cancellationToken);

    Task<Result<SignedIn<AuthUserDto>>> VerifyAsync(VerifyEmailCodeRequest request, CancellationToken cancellationToken);
}

public sealed class RegistrationService(
    IAccountRepository accounts,
    IPendingRegistrationRepository pending,
    IConsentRepository consents,
    ICodeService codes,
    ISessionService sessions,
    IPasswordHasher passwords,
    INotificationSender notifications,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<IdentityServiceOptions> options) : IRegistrationService
{
    private const string Source = "sign-up";

    public async Task<Result<EmailChallengeDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!request.Consent)
        {
            return IdentityErrors.ConsentRequired;
        }

        var email = EmailAddress.Normalize(request.Email);
        if (email is null)
        {
            return IdentityErrors.InvalidEmail;
        }

        if (await accounts.EmailInUseAsync(email, null, cancellationToken))
        {
            return IdentityErrors.EmailTaken;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var passwordHash = passwords.Hash(request.Password);
        var policy = options.Value.PrivacyPolicyVersion;
        var registration = await pending.FindAsync(email, cancellationToken);
        if (registration is null)
        {
            pending.Add(PendingRegistration.Start(tenant.TenantId, email, request.FirstName, request.LastName, passwordHash, policy, now));
        }
        else
        {
            registration.Update(request.FirstName, request.LastName, passwordHash, policy, now);
        }

        return await SendCodeAsync(email, cancellationToken);
    }

    public async Task<Result<EmailChallengeDto>> ResendAsync(SendEmailCodeRequest request, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Normalize(request.Email);
        if (email is null)
        {
            return IdentityErrors.InvalidEmail;
        }

        return await pending.FindAsync(email, cancellationToken) is null
            ? IdentityErrors.RegistrationNotFound
            : await SendCodeAsync(email, cancellationToken);
    }

    public async Task<Result<SignedIn<AuthUserDto>>> VerifyAsync(VerifyEmailCodeRequest request, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Normalize(request.Email);
        if (email is null)
        {
            return IdentityErrors.InvalidEmail;
        }

        var registration = await pending.FindAsync(email, cancellationToken);
        if (registration is null)
        {
            return IdentityErrors.RegistrationNotFound;
        }

        var check = await codes.VerifyAsync(CodePurpose.RegisterEmail, email, request.Code, cancellationToken);
        if (!check.IsSuccess)
        {
            return check.Error;
        }

        if (await accounts.EmailInUseAsync(email, null, cancellationToken))
        {
            return IdentityErrors.EmailTaken;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var market = Market.For(tenant.Market);
        var account = Account.RegisterWithEmail(
            tenant.TenantId,
            market.Code,
            registration.FirstName,
            registration.LastName,
            email,
            registration.PasswordHash,
            now);
        accounts.Add(account);
        consents.Add(ConsentRecord.Record(
            tenant.TenantId,
            account.Id,
            ConsentRecord.RegistrationScope,
            registration.ConsentPolicyVersion,
            market.PrivacyLaw,
            granted: true,
            Source,
            now));
        pending.Remove(registration);
        outbox.Add(new LearnerRegistered(account.Id, account.FirstName, account.LastName, email, null, market.Code, Source)
        {
            TenantId = tenant.TenantId,
        });

        // One SaveChanges: account, consent, pending removal, outbox row and session commit together.
        return await sessions.SignInAsync(account, cancellationToken);
    }

    private async Task<Result<EmailChallengeDto>> SendCodeAsync(string email, CancellationToken cancellationToken)
    {
        var challenge = await codes.IssueAsync(CodePurpose.RegisterEmail, email, null, reuseUsable: false, cancellationToken);
        if (!challenge.IsSuccess)
        {
            return challenge.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var sent = challenge.Value;
        if (sent.CodeToSend is { } code)
        {
            await notifications.SendAsync(
                new CodeNotification(tenant.TenantId, NotificationChannel.Email, email, NotificationTemplates.SignupCode, code, sent.ValidForMinutes),
                cancellationToken);
        }

        return new EmailChallengeDto(email, sent.ResendIn, sent.ValidForMinutes, sent.TriesLeft);
    }
}
