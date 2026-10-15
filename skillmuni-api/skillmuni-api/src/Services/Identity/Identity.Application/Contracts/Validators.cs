using FluentValidation;
using Identity.Domain.Common;

namespace Identity.Application.Contracts;

// Shape checks only (422 "validation"). Business rules (unknown number, wrong code…) live in the services.

internal static class Rules
{
    public const int MaxPhoneLength = 20;
    public const int MaxCodeLength = 12;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MaxNameLength = 100;

    public static IRuleBuilderOptions<T, string> Code<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Enter the code.").MaximumLength(MaxCodeLength);

    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Enter your email.")
            .MaximumLength(EmailAddress.MaxLength)
            .Must(e => EmailAddress.Normalize(e) is not null).WithMessage("That doesn't look like an email address.");

    public static IRuleBuilderOptions<T, string> Phone<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Enter your mobile number.").MaximumLength(MaxPhoneLength);

    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(MinPasswordLength).WithMessage($"Use at least {MinPasswordLength} characters.")
            .MaximumLength(MaxPasswordLength);
}

public sealed class SendPhoneCodeRequestValidator : AbstractValidator<SendPhoneCodeRequest>
{
    public SendPhoneCodeRequestValidator() => RuleFor(r => r.Phone).Phone();
}

public sealed class VerifyPhoneCodeRequestValidator : AbstractValidator<VerifyPhoneCodeRequest>
{
    public VerifyPhoneCodeRequestValidator()
    {
        RuleFor(r => r.Phone).Phone();
        RuleFor(r => r.Code).Code();
    }
}

public sealed class SendEmailCodeRequestValidator : AbstractValidator<SendEmailCodeRequest>
{
    public SendEmailCodeRequestValidator() => RuleFor(r => r.Email).Email();
}

public sealed class VerifyEmailCodeRequestValidator : AbstractValidator<VerifyEmailCodeRequest>
{
    public VerifyEmailCodeRequestValidator()
    {
        RuleFor(r => r.Email).Email();
        RuleFor(r => r.Code).Code();
    }
}

public sealed class PasswordLoginRequestValidator : AbstractValidator<PasswordLoginRequest>
{
    public PasswordLoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(EmailAddress.MaxLength);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(Rules.MaxPasswordLength);
    }
}

public sealed class PasswordResetRequestValidator : AbstractValidator<PasswordResetRequest>
{
    public PasswordResetRequestValidator() => RuleFor(r => r.Email).Email();
}

public sealed class PasswordResetConfirmRequestValidator : AbstractValidator<PasswordResetConfirmRequest>
{
    public PasswordResetConfirmRequestValidator()
    {
        RuleFor(r => r.Token).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Password).Password();
    }
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.FirstName).NotEmpty().WithMessage("Enter your first name.").MaximumLength(Rules.MaxNameLength);
        RuleFor(r => r.LastName).MaximumLength(Rules.MaxNameLength);
        RuleFor(r => r.Email).Email();
        RuleFor(r => r.Password).Password();
    }
}

public sealed class GoogleSignInRequestValidator : AbstractValidator<GoogleSignInRequest>
{
    public GoogleSignInRequestValidator() => RuleFor(r => r.Credential).NotEmpty().MaximumLength(4096);
}

public sealed class RefreshSessionRequestValidator : AbstractValidator<RefreshSessionRequest>
{
    public RefreshSessionRequestValidator() => RuleFor(r => r.RefreshToken).NotEmpty().MaximumLength(200);
}

public sealed class ContactCodeRequestValidator : AbstractValidator<ContactCodeRequest>
{
    public ContactCodeRequestValidator()
    {
        RuleFor(r => r.Kind).Must(ContactKinds.IsKnown).WithMessage("Kind must be mobile or email.");
        RuleFor(r => r.Value).NotEmpty().MaximumLength(EmailAddress.MaxLength);
    }
}

public sealed class ContactVerifyRequestValidator : AbstractValidator<ContactVerifyRequest>
{
    public ContactVerifyRequestValidator()
    {
        RuleFor(r => r.Kind).Must(ContactKinds.IsKnown).WithMessage("Kind must be mobile or email.");
        RuleFor(r => r.Value).NotEmpty().MaximumLength(EmailAddress.MaxLength);
        RuleFor(r => r.Code).Code();
    }
}

public sealed class ClaimCodeRequestValidator : AbstractValidator<ClaimCodeRequest>
{
    public ClaimCodeRequestValidator() => RuleFor(r => r.Code).Code();
}

public sealed class ClaimDeclineRequestValidator : AbstractValidator<ClaimDeclineRequest>
{
    private static readonly string[] Reasons = ["not-me", "not-now", "has-account", "other"];

    public ClaimDeclineRequestValidator() =>
        RuleFor(r => r.Reason).Must(r => r is null || Reasons.Contains(r)).WithMessage("Unknown reason.");
}

public sealed class RecordConsentRequestValidator : AbstractValidator<RecordConsentRequest>
{
    public RecordConsentRequestValidator()
    {
        RuleFor(r => r.Scope).NotEmpty().MaximumLength(64).Matches("^[a-z0-9.-]+$").WithMessage("Use lower-case letters, digits, dots and dashes.");
        RuleFor(r => r.PolicyVersion).NotEmpty().MaximumLength(32);
    }
}

public sealed class CreateClaimRequestValidator : AbstractValidator<CreateClaimRequest>
{
    public CreateClaimRequestValidator()
    {
        RuleFor(r => r.Institute).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Programme).NotEmpty().MaximumLength(300);
        RuleFor(r => r.Batch).MaximumLength(300);
        RuleFor(r => r.FromCv).MaximumLength(300);
        RuleFor(r => r.Certificate).MaximumLength(200);
        RuleFor(r => r.Phone).Phone();
        RuleFor(r => r.Sections).NotEmpty();
        RuleForEach(r => r.Sections).ChildRules(s =>
        {
            s.RuleFor(x => x.Slug).NotEmpty().MaximumLength(64);
            s.RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
        });
    }
}

public static class ContactKinds
{
    public const string Mobile = "mobile";
    public const string Email = "email";

    public static bool IsKnown(string? kind) => kind is Mobile or Email;
}
