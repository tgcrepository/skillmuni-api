using FluentValidation;

namespace Learning.Application.Contracts;

// Shape checks only (422 "validation"). Business rules (unknown quiz, locked brief…) live in the services.

public sealed class BookmarkRequestValidator : AbstractValidator<BookmarkRequest>
{
    public BookmarkRequestValidator() => RuleFor(r => r.On).NotNull().WithMessage("Send on: true or false.");
}

public sealed class QuizAnswersRequestValidator : AbstractValidator<QuizAnswersRequest>
{
    public const int MaxAnswers = 50;
    public const int MaxIdLength = 80;

    public QuizAnswersRequestValidator()
    {
        RuleFor(r => r.Answers).NotNull().WithMessage("Send the answers as an object.");
        RuleFor(r => r.Answers!.Count).LessThanOrEqualTo(MaxAnswers).When(r => r.Answers is not null)
            .OverridePropertyName("answers").WithMessage($"Send at most {MaxAnswers} answers.");
        RuleFor(r => r.Answers)
            .Must(a => a!.All(kv => kv.Key.Length is > 0 and <= MaxIdLength && kv.Value is { Length: <= MaxIdLength }))
            .When(r => r.Answers is not null)
            .WithMessage("Each answer is a question id and an option id.");
    }
}

public sealed class ActivityRequestValidator : AbstractValidator<ActivityRequest>
{
    public const int MaxMinutes = 60;

    public ActivityRequestValidator() =>
        RuleFor(r => r.Minutes).NotNull().InclusiveBetween(1, MaxMinutes).WithMessage($"Send 1 to {MaxMinutes} minutes.");
}
