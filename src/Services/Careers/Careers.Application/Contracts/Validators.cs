using FluentValidation;

namespace Careers.Application.Contracts;

// 422 "validation" with one entry per field (ValidationFilter). Business rules (gates, seats) live in the services.

public sealed class ApplyRequestValidator : AbstractValidator<ApplyRequest>
{
    public ApplyRequestValidator()
    {
        RuleFor(r => r.CvId).NotEmpty().WithMessage("Pick a CV.");
        RuleFor(r => r.Answers)
            .Must(a => a is null || (a.Count <= 20 && a.All(kv => kv.Key.Length <= 40 && (kv.Value?.Length ?? 0) <= 1000)))
            .WithMessage("Keep each answer under 1,000 characters.");
        RuleFor(r => r.InvitationId).MaximumLength(64);
    }
}

public sealed class RespondRequestValidator : AbstractValidator<RespondRequest>
{
    public RespondRequestValidator()
    {
        RuleFor(r => r.Note).MaximumLength(500).WithMessage("Keep the note under 500 characters.");
    }
}

public sealed class AlertSettingsValidator : AbstractValidator<AlertSettingsDto>
{
    public AlertSettingsValidator()
    {
        RuleFor(r => r.Frequency).Must(f => f is "daily" or "weekly").WithMessage("Daily or weekly.");
        RuleFor(r => r.Searches).NotNull().Must(s => s.Count <= 10).WithMessage("Up to 10 saved searches.");
        RuleForEach(r => r.Searches).NotEmpty().MaximumLength(100);
    }
}

public sealed class BookRequestValidator : AbstractValidator<BookRequest>
{
    public BookRequestValidator()
    {
        RuleFor(r => r.Picks).NotEmpty().WithMessage("Pick an interview time.");
        RuleForEach(r => r.Picks).ChildRules(pick =>
        {
            pick.RuleFor(p => p.EmployerId).NotEmpty().MaximumLength(40);
            pick.RuleFor(p => p.Date).Must(d => DateOnly.TryParseExact(d, "yyyy-MM-dd", out _)).WithMessage("Use a date like 2026-12-01.");
            pick.RuleFor(p => p.Time).Matches("^([01][0-9]|2[0-3]):[0-5][0-9]$").WithMessage("Use a time like 10:30.");
        });
        RuleFor(r => r.Reminders).NotNull();
        RuleFor(r => r.CvTemplate).NotNull().MaximumLength(40);
    }
}

public sealed class CancelRequestValidator : AbstractValidator<CancelRequest>
{
    public CancelRequestValidator()
    {
        RuleFor(r => r.EmployerId).MaximumLength(40);
        RuleFor(r => r.Reason).MaximumLength(500);
    }
}

public sealed class WaitlistRequestValidator : AbstractValidator<WaitlistRequest>
{
    public WaitlistRequestValidator()
    {
        RuleFor(r => r.EmployerId).NotEmpty().MaximumLength(40);
    }
}

public sealed class OutcomeRequestValidator : AbstractValidator<OutcomeRequest>
{
    public OutcomeRequestValidator()
    {
        RuleFor(r => r.EmployerId).NotEmpty().MaximumLength(40);
        RuleFor(r => r.Outcome).Must(o => o is null or "offer" or "waiting" or "no").WithMessage("Offer, waiting or no.");
    }
}

public sealed class RatingRequestValidator : AbstractValidator<RatingRequest>
{
    public RatingRequestValidator()
    {
        RuleFor(r => r.Stars).InclusiveBetween(1, 5).WithMessage("Rate 1 to 5 stars.");
    }
}
