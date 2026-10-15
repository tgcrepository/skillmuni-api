using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Application.Errors;
using Identity.Application.Events;
using Identity.Domain.Codes;
using Identity.Domain.Common;
using SkillMuni.Common.Messaging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Identity.Application.Services;

/// <summary>
/// Verifying a signed-in learner's contact details: Quest 2 "Hit Me Up" (<c>/me/phone</c>) and Edit profile
/// (<c>/profile/contact</c>, A-273). A new value is saved only once its code matches; a verified mobile can log in.
/// </summary>
public interface IContactService
{
    Task<Result<OtpChallengeDto>> SendPhoneCodeAsync(Guid accountId, SendPhoneCodeRequest request, CancellationToken cancellationToken);

    Task<Result> VerifyPhoneAsync(Guid accountId, VerifyPhoneCodeRequest request, CancellationToken cancellationToken);

    Task<Result<ContactCodeSentDto>> SendContactCodeAsync(Guid accountId, ContactCodeRequest request, CancellationToken cancellationToken);

    Task<Result> VerifyContactAsync(Guid accountId, ContactVerifyRequest request, CancellationToken cancellationToken);
}

public sealed class ContactService(
    IAccountRepository accounts,
    ICodeService codes,
    INotificationSender notifications,
    IOutbox outbox,
    IUnitOfWork unitOfWork,
    ITenantContext tenant,
    TimeProvider clock) : IContactService
{
    private sealed record Target(string Kind, string Value, CodePurpose Purpose, string CodeTarget);

    private sealed record Sent(Target Target, CodeChallenge Challenge);

    public async Task<Result<OtpChallengeDto>> SendPhoneCodeAsync(Guid accountId, SendPhoneCodeRequest request, CancellationToken cancellationToken)
    {
        var sent = await SendAsync(accountId, ContactKinds.Mobile, request.Phone, cancellationToken);
        if (!sent.IsSuccess)
        {
            return sent.Error;
        }

        var market = Market.For(tenant.Market);
        return new OtpChallengeDto(market.FormatMobile(sent.Value.Target.Value), sent.Value.Challenge.ResendIn, sent.Value.Challenge.TriesLeft);
    }

    public Task<Result> VerifyPhoneAsync(Guid accountId, VerifyPhoneCodeRequest request, CancellationToken cancellationToken) =>
        VerifyAsync(accountId, ContactKinds.Mobile, request.Phone, request.Code, cancellationToken);

    public async Task<Result<ContactCodeSentDto>> SendContactCodeAsync(Guid accountId, ContactCodeRequest request, CancellationToken cancellationToken)
    {
        var sent = await SendAsync(accountId, request.Kind, request.Value, cancellationToken);
        if (!sent.IsSuccess)
        {
            return sent.Error;
        }

        var target = sent.Value.Target;
        var masked = target.Kind == ContactKinds.Mobile
            ? Market.For(tenant.Market).MaskMobile(target.Value)
            : EmailAddress.Mask(target.Value);
        return new ContactCodeSentDto(masked);
    }

    public Task<Result> VerifyContactAsync(Guid accountId, ContactVerifyRequest request, CancellationToken cancellationToken) =>
        VerifyAsync(accountId, request.Kind, request.Value, request.Code, cancellationToken);

    private async Task<Result<Sent>> SendAsync(Guid accountId, string kind, string value, CancellationToken cancellationToken)
    {
        var target = Resolve(accountId, kind, value);
        if (!target.IsSuccess)
        {
            return target.Error;
        }

        var taken = await InUseAsync(target.Value, accountId, cancellationToken);
        if (taken is not null)
        {
            return taken;
        }

        var challenge = await codes.IssueAsync(target.Value.Purpose, target.Value.CodeTarget, accountId, reuseUsable: false, cancellationToken);
        if (!challenge.IsSuccess)
        {
            return challenge.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (challenge.Value.CodeToSend is { } code)
        {
            var channel = target.Value.Kind == ContactKinds.Mobile ? NotificationChannel.Sms : NotificationChannel.Email;
            await notifications.SendAsync(
                new CodeNotification(tenant.TenantId, channel, target.Value.Value, NotificationTemplates.ContactCode, code, challenge.Value.ValidForMinutes),
                cancellationToken);
        }

        return new Sent(target.Value, challenge.Value);
    }

    private async Task<Result> VerifyAsync(Guid accountId, string kind, string value, string code, CancellationToken cancellationToken)
    {
        var target = Resolve(accountId, kind, value);
        if (!target.IsSuccess)
        {
            return target.Error;
        }

        var check = await codes.VerifyAsync(target.Value.Purpose, target.Value.CodeTarget, code, cancellationToken);
        if (!check.IsSuccess)
        {
            return check;
        }

        var taken = await InUseAsync(target.Value, accountId, cancellationToken);
        if (taken is not null)
        {
            return taken;
        }

        var account = await accounts.GetAsync(accountId, cancellationToken);
        if (account is null || account.IsErased)
        {
            return IdentityErrors.NoSession;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (target.Value.Kind == ContactKinds.Mobile)
        {
            account.ChangePhone(target.Value.Value, now);
        }
        else
        {
            account.ChangeEmail(target.Value.Value, now);
        }

        outbox.Add(new ContactVerified(account.Id, target.Value.Kind, target.Value.Value) { TenantId = account.TenantId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private Result<Target> Resolve(Guid accountId, string kind, string value)
    {
        if (kind == ContactKinds.Mobile)
        {
            var phone = Market.For(tenant.Market).NormalizeMobile(value);
            return phone is null
                ? IdentityErrors.InvalidPhone
                : new Target(ContactKinds.Mobile, phone, CodePurpose.ContactPhone, $"{accountId:N}:{phone}");
        }

        var email = EmailAddress.Normalize(value);
        return email is null
            ? IdentityErrors.InvalidEmail
            : new Target(ContactKinds.Email, email, CodePurpose.ContactEmail, $"{accountId:N}:{email}");
    }

    private async Task<Error?> InUseAsync(Target target, Guid accountId, CancellationToken cancellationToken)
    {
        if (target.Kind == ContactKinds.Mobile)
        {
            return await accounts.PhoneInUseAsync(target.Value, accountId, cancellationToken) ? IdentityErrors.PhoneTaken : null;
        }

        return await accounts.EmailInUseAsync(target.Value, accountId, cancellationToken) ? IdentityErrors.EmailTaken : null;
    }
}
