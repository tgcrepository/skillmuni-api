using System.Globalization;
using System.Text.Json;
using FluentValidation;

namespace Learner.Application.Contracts;

// Shape checks only (422 "validation"). Business rules (unknown code, marks too low…) live in the services.

internal static class Vocab
{
    public static readonly string[] DigiDocs = ["aadhaar", "pan", "driving-licence"];

    public static readonly string[] Genders = ["female", "male", "non-binary", "rather-not"];

    public static readonly string[] PrivateFields = ["mobile", "email", "gender", "dob", "id"];

    public static readonly string[] Proficiencies = ["basic", "okay", "good", "pro"];

    public static readonly string[] AccountTypes = ["savings", "current", "cheque"];

    public static readonly string[] MarksFrom = ["10", "12"];

    public static bool IsPercent(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is >= 0 and <= 100;

    public static bool IsIsoDateOrEmpty(string? value) =>
        string.IsNullOrEmpty(value) || DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static bool IsWebUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public static IRuleBuilderOptions<T, string> OneOf<T>(this IRuleBuilder<T, string> rule, string[] allowed) =>
        rule.NotEmpty().Must(v => allowed.Contains(v, StringComparer.Ordinal)).WithMessage($"Use one of: {string.Join(", ", allowed)}.");
}

public sealed class SavePartRequestValidator : AbstractValidator<SavePartRequest>
{
    public SavePartRequestValidator()
    {
        RuleFor(r => r.Values).Must(v => v.ValueKind == JsonValueKind.Object).WithMessage("Send the part's values as an object.");
        RuleFor(r => r.BonusXp).GreaterThanOrEqualTo(0).When(r => r.BonusXp is not null);
    }
}

public sealed class StartDigiLockerRequestValidator : AbstractValidator<StartDigiLockerRequest>
{
    public StartDigiLockerRequestValidator() => RuleFor(r => r.Doc).OneOf(Vocab.DigiDocs);
}

public sealed class LinkDigiLockerRequestValidator : AbstractValidator<LinkDigiLockerRequest>
{
    public LinkDigiLockerRequestValidator()
    {
        RuleFor(r => r.Doc).OneOf(Vocab.DigiDocs);
        RuleFor(r => r.Code).NotEmpty().MaximumLength(512);
    }
}

public sealed class ProgrammeSubmissionValidator : AbstractValidator<ProgrammeSubmission>
{
    public ProgrammeSubmissionValidator()
    {
        RuleFor(r => r.Code).NotEmpty().MaximumLength(40);
        RuleFor(r => r.MarksFrom).OneOf(Vocab.MarksFrom);
        RuleFor(r => r.English).Must(Vocab.IsPercent).WithMessage("Enter a percentage from 0 to 100.");
        RuleFor(r => r.Maths).Must(Vocab.IsPercent).WithMessage("Enter a percentage from 0 to 100.");
        RuleFor(r => r.Proficiency)
            .Must(p => p is null || (p.Count <= 20 && p.All(kv => kv.Key.Length is > 0 and <= 60 && Vocab.Proficiencies.Contains(kv.Value))))
            .WithMessage("Pick basic, okay, good or pro for each skill.");
        When(r => r.Bank is not null, () =>
        {
            RuleFor(r => r.Bank!.Holder).NotEmpty().MaximumLength(100).OverridePropertyName("bank.holder");
            RuleFor(r => r.Bank!.Type).OneOf(Vocab.AccountTypes).OverridePropertyName("bank.type");
            RuleFor(r => r.Bank!.Account).NotEmpty().Matches("^[0-9]{6,18}$").WithMessage("Enter the account number (digits only).")
                .OverridePropertyName("bank.account");
            RuleFor(r => r.Bank!.Routing).NotEmpty().MaximumLength(20).OverridePropertyName("bank.routing");
            RuleFor(r => r.Bank!.Upi).MaximumLength(100).Matches("^[A-Za-z0-9._-]+@[A-Za-z0-9.-]+$")
                .When(r => !string.IsNullOrEmpty(r.Bank!.Upi)).WithMessage("That doesn't look like a UPI id.")
                .OverridePropertyName("bank.upi");
        });
    }
}

public sealed class SaveProgrammeRequestValidator : AbstractValidator<SaveProgrammeRequest>
{
    public SaveProgrammeRequestValidator()
    {
        RuleFor(r => r.Code).NotEmpty().MaximumLength(40).Matches("^[A-Za-z0-9-]+$");
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Partner).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Centre).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Duration).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Stipend).MaximumLength(100);
        RuleFor(r => r.MarksFrom).OneOf(Vocab.MarksFrom);
        RuleFor(r => r.MinEnglish).InclusiveBetween(0, 100);
        RuleFor(r => r.MinMaths).InclusiveBetween(0, 100);
        RuleFor(r => r.Eligibility).NotEmpty().MaximumLength(200);
    }
}

public sealed class FeedbackRequestValidator : AbstractValidator<FeedbackRequest>
{
    public FeedbackRequestValidator()
    {
        RuleFor(r => r.Mood).InclusiveBetween(1, 5);
        RuleFor(r => r.Tags).Must(t => t is null || (t.Count <= 10 && t.All(x => x.Length is > 0 and <= 40)))
            .WithMessage("Up to 10 short tags.");
        RuleFor(r => r.Comment).MaximumLength(2000);
        RuleFor(r => r.Screen).MaximumLength(200);
    }
}

public sealed class ProfileEditRequestValidator : AbstractValidator<ProfileEditRequest>
{
    public ProfileEditRequestValidator()
    {
        RuleFor(r => r.Skills).NotNull()
            .Must(s => s.Count <= Domain.LearnerProfile.MaxProfileSkills).WithMessage($"Pick up to {Domain.LearnerProfile.MaxProfileSkills} skills.")
            .Must(s => s.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 60)).WithMessage("Skill names are 1–60 characters.");
        RuleFor(r => r.Gender).OneOf(Vocab.Genders);
        RuleFor(r => r.Dob).Must(Vocab.IsIsoDateOrEmpty).WithMessage("Use a date like 2004-08-21.");
    }
}

public sealed class RevealRequestValidator : AbstractValidator<RevealRequest>
{
    public RevealRequestValidator() => RuleFor(r => r.Field).OneOf(Vocab.PrivateFields);
}

public sealed class CheckLinkRequestValidator : AbstractValidator<CheckLinkRequest>
{
    public CheckLinkRequestValidator() =>
        RuleFor(r => r.Url).NotEmpty().MaximumLength(2048).Must(Vocab.IsWebUrl).WithMessage("Paste the full link, starting with https://.");
}

public sealed class PreferencesRequestValidator : AbstractValidator<PreferencesRequest>
{
    public PreferencesRequestValidator() =>
        RuleFor(r => r.Language).NotEmpty().Matches("^[a-z]{2,3}(-[A-Z]{2})?$").WithMessage("Use a language code like en or hi-IN.");
}

public sealed class SaveAboutRequestValidator : AbstractValidator<SaveAboutRequest>
{
    public SaveAboutRequestValidator() =>
        RuleFor(r => r.Text).MaximumLength(Domain.JobProfileSettings.MaxAboutLength)
            .WithMessage($"Keep it to {Domain.JobProfileSettings.MaxAboutLength} characters.");
}

public sealed class ApplyCvChangesRequestValidator : AbstractValidator<ApplyCvChangesRequest>
{
    public const int MaxIds = 50;

    public ApplyCvChangesRequestValidator()
    {
        RuleFor(r => r.Ids).NotNull().WithMessage("Send the ids of the changes to add, or an empty list.")
            .Must(ids => ids is null || ids.Count <= MaxIds).WithMessage($"Up to {MaxIds} changes at a time.");
        RuleForEach(r => r.Ids).NotEmpty().MaximumLength(120);
    }
}

public sealed class UpdateShareRequestValidator : AbstractValidator<UpdateShareRequest>
{
    public UpdateShareRequestValidator(Microsoft.Extensions.Options.IOptions<LearnerServiceOptions> options)
    {
        var allowed = options.Value.JobProfile.Expiries();
        RuleFor(r => r.ExpiresDays).Must(d => allowed.Contains(d))
            .WithMessage($"Links last {string.Join(", ", allowed)} days.");
    }
}
