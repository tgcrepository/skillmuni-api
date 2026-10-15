using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Application.Errors;
using Identity.Domain.Codes;
using Identity.Domain.Common;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Identity.Application.Services;

/// <summary>
/// Login by mobile code, email code (A-159) or password, and password reset. A send-code call for a mobile or
/// email with no account returns 404 account-not-found so the app can offer sign-up (BR-136, A-308).
/// </summary>
public interface ILoginService
{
    Task<Result<OtpChallengeDto>> SendPhoneCodeAsync(SendPhoneCodeRequest request, CancellationToken cancellationToken);

    Task<Result<SignedIn<AuthUserDto>>> VerifyPhoneCodeAsync(VerifyPhoneCodeRequest request, CancellationToken cancellationToken);

    Task<Result<OtpChallengeDto>> SendEmailCodeAsync(SendEmailCodeRequest request, CancellationToken cancellationToken);

    Task<Result<SignedIn<AuthUserDto>>> VerifyEmailCodeAsync(VerifyEmailCodeRequest request, CancellationToken cancellationToken);

    Task<Result<SignedIn<AuthUserDto>>> PasswordLoginAsync(PasswordLoginRequest request, CancellationToken cancellationToken);

    /// <summary>Always the same answer, so the endpoint can't be used to find out who has an account.</summary>
    Task<ResetRequestedDto> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken);

    Task<Result> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request, CancellationToken cancellationToken);
}

public sealed class LoginService(
    IAccountRepository accounts,
    IVerificationCodeRepository codeRepository,
    ICodeService codes,
    ISessionService sessions,
    IPasswordHasher passwords,
    ICodeGenerator generator,
    ISecretHasher hasher,
    INotificationSender notifications,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock,
    IOptions<IdentityServiceOptions> options) : ILoginService
{
    private readonly IdentityServiceOptions _options = options.Value;

    public async Task<Result<OtpChallengeDto>> SendPhoneCodeAsync(SendPhoneCodeRequest request, CancellationToken cancellationToken)
    {
        var market = Market.For(tenant.Market);
        var phone = market.NormalizeMobile(request.Phone);
        if (phone is null)
        {
            return IdentityErrors.InvalidPhone;
        }

        var account = await accounts.FindByPhoneAsync(phone, cancellationToken);
        if (account is null)
        {
            return IdentityErrors.AccountNotFound;
        }

        if (account.IsLocked(Now))
        {
            return IdentityErrors.AccountLocked;
        }

        var challenge = await codes.IssueAsync(CodePurpose.LoginPhone, phone, account.Id, reuseUsable: false, cancellationToken);
        if (!challenge.IsSuccess)
        {
            return challenge.Error;
        }

        await SaveAndSendAsync(challenge.Value, NotificationChannel.Sms, phone, NotificationTemplates.LoginCode, cancellationToken);
        return new OtpChallengeDto(market.FormatMobile(phone), challenge.Value.ResendIn, challenge.Value.TriesLeft);
    }

    public async Task<Result<SignedIn<AuthUserDto>>> VerifyPhoneCodeAsync(VerifyPhoneCodeRequest request, CancellationToken cancellationToken)
    {
        var phone = Market.For(tenant.Market).NormalizeMobile(request.Phone);
        if (phone is null)
        {
            return IdentityErrors.InvalidPhone;
        }

        var check = await codes.VerifyAsync(CodePurpose.LoginPhone, phone, request.Code, cancellationToken);
        if (!check.IsSuccess)
        {
            return check.Error;
        }

        var account = await accounts.FindByPhoneAsync(phone, cancellationToken);
        if (account is null)
        {
            return IdentityErrors.AccountNotFound;
        }

        return account.IsLocked(Now)
            ? IdentityErrors.AccountLocked
            : await sessions.SignInAsync(account, cancellationToken);
    }

    public async Task<Result<OtpChallengeDto>> SendEmailCodeAsync(SendEmailCodeRequest request, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Normalize(request.Email);
        if (email is null)
        {
            return IdentityErrors.InvalidEmail;
        }

        var account = await accounts.FindByEmailAsync(email, cancellationToken);
        if (account is null)
        {
            return IdentityErrors.AccountNotFound;
        }

        if (account.IsLocked(Now))
        {
            return IdentityErrors.AccountLocked;
        }

        var challenge = await codes.IssueAsync(CodePurpose.LoginEmail, email, account.Id, reuseUsable: false, cancellationToken);
        if (!challenge.IsSuccess)
        {
            return challenge.Error;
        }

        await SaveAndSendAsync(challenge.Value, NotificationChannel.Email, email, NotificationTemplates.LoginCode, cancellationToken);
        return new OtpChallengeDto(EmailAddress.Mask(email), challenge.Value.ResendIn, challenge.Value.TriesLeft);
    }

    public async Task<Result<SignedIn<AuthUserDto>>> VerifyEmailCodeAsync(VerifyEmailCodeRequest request, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Normalize(request.Email);
        if (email is null)
        {
            return IdentityErrors.InvalidEmail;
        }

        var check = await codes.VerifyAsync(CodePurpose.LoginEmail, email, request.Code, cancellationToken);
        if (!check.IsSuccess)
        {
            return check.Error;
        }

        var account = await accounts.FindByEmailAsync(email, cancellationToken);
        if (account is null)
        {
            return IdentityErrors.AccountNotFound;
        }

        return account.IsLocked(Now)
            ? IdentityErrors.AccountLocked
            : await sessions.SignInAsync(account, cancellationToken);
    }

    public async Task<Result<SignedIn<AuthUserDto>>> PasswordLoginAsync(PasswordLoginRequest request, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Normalize(request.Email);
        var account = email is null ? null : await accounts.FindByEmailAsync(email, cancellationToken);
        if (account?.PasswordHash is null)
        {
            return IdentityErrors.WrongCredentials;
        }

        var now = Now;
        if (account.IsLocked(now))
        {
            return IdentityErrors.AccountLocked;
        }

        // B-46: no lock for wrong passwords (the BRD has none); the gateway's per-IP and per-account limits apply.
        if (!passwords.Verify(account.PasswordHash, request.Password))
        {
            return IdentityErrors.WrongCredentials;
        }

        return await sessions.SignInAsync(account, cancellationToken);
    }

    public async Task<ResetRequestedDto> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken)
    {
        var answer = new ResetRequestedDto((int)_options.ResetResendAfter.TotalSeconds, (int)_options.ResetLinkLifetime.TotalMinutes);
        var email = EmailAddress.Normalize(request.Email);
        var account = email is null ? null : await accounts.FindByEmailAsync(email, cancellationToken);
        if (email is null || account is null)
        {
            return answer;
        }

        var now = Now;
        var latest = await codeRepository.FindLatestAsync(CodePurpose.PasswordReset, email, cancellationToken);
        if (latest is not null && !latest.CanResend(now))
        {
            return answer with { ResendIn = latest.SecondsUntilResend(now) };
        }

        latest?.Supersede(now);
        var token = generator.NewToken();
        codeRepository.Add(VerificationCode.Issue(
            account.TenantId,
            CodePurpose.PasswordReset,
            email,
            hasher.Hash(token),
            account.Id,
            now,
            _options.ResetLinkLifetime,
            _options.ResetResendAfter,
            tries: 1));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifications.SendAsync(
            new CodeNotification(tenant.TenantId, NotificationChannel.Email, email, NotificationTemplates.PasswordReset, token, answer.ValidFor),
            cancellationToken);
        return answer;
    }

    public async Task<Result> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request, CancellationToken cancellationToken)
    {
        var now = Now;
        var reset = await codeRepository.FindByHashAsync(CodePurpose.PasswordReset, hasher.Hash(request.Token), cancellationToken);
        if (reset?.AccountId is not { } accountId || !reset.IsUsable(now))
        {
            return IdentityErrors.CodeExpired;
        }

        var account = await accounts.GetAsync(accountId, cancellationToken);
        if (account is null || account.IsErased)
        {
            return IdentityErrors.CodeExpired;
        }

        reset.Check(matches: true, now);
        account.ChangePassword(passwords.Hash(request.Password), now);
        await sessions.RevokeAllAsync(account.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private async Task SaveAndSendAsync(CodeChallenge challenge, NotificationChannel channel, string to, string template, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (challenge.CodeToSend is { } code)
        {
            await notifications.SendAsync(
                new CodeNotification(tenant.TenantId, channel, to, template, code, challenge.ValidForMinutes),
                cancellationToken);
        }
    }
}
