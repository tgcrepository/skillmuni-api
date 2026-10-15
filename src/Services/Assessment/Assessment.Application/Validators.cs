using FluentValidation;

namespace Assessment.Application;

// Shape checks only (422 "validation"). Submit has none: an incomplete submit is the business code 422 "incomplete".

public sealed class SaveAnswersRequestValidator : AbstractValidator<SaveAnswersRequest>
{
    public SaveAnswersRequestValidator(ICareerDriversContentProvider content)
    {
        var pairs = content.Content.Pairs.Count;
        RuleFor(x => x.Answers)
            .NotNull().WithMessage("Send every pair's answer.")
            .Must(a => a!.Count == pairs).WithMessage($"Send one answer per pair ({pairs}).")
            .When(x => x.Answers is not null, ApplyConditionTo.CurrentValidator);
        RuleFor(x => x.Current)
            .NotNull().WithMessage("Send the pair to show next.")
            .InclusiveBetween(0, pairs).WithMessage($"Current is 0 to {pairs}.");
    }
}

public sealed class RemindRequestValidator : AbstractValidator<RemindRequest>
{
    public RemindRequestValidator()
    {
        RuleFor(x => x.Remind).NotNull().WithMessage("Send remind: true or false.");
    }
}
