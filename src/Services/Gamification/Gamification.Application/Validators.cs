using FluentValidation;

namespace Gamification.Application;

// Shape checks only (422 "validation"; malformed JSON is 400). Business rules (badge not earned…) live in the services.

public sealed class ShowcaseRequestValidator : AbstractValidator<ShowcaseRequest>
{
    public const int MaxIdLength = 64;

    public ShowcaseRequestValidator()
    {
        RuleFor(r => r.Ids).NotNull().WithMessage("Send the badge ids to pin, or an empty list.");
        RuleForEach(r => r.Ids).NotEmpty().MaximumLength(MaxIdLength);
    }
}

public sealed class ArenaQueryValidator : AbstractValidator<ArenaQuery>
{
    public static readonly string[] Scopes = ["everyone", "batch", "friends"];

    public static readonly string[] Periods = ["week", "month", "all"];

    public ArenaQueryValidator()
    {
        RuleFor(q => q.Scope)
            .Must(s => s is null || Scopes.Contains(s, StringComparer.Ordinal))
            .WithMessage($"Use one of: {string.Join(", ", Scopes)}.");
        RuleFor(q => q.Period)
            .Must(p => p is null || Periods.Contains(p, StringComparer.Ordinal))
            .WithMessage($"Use one of: {string.Join(", ", Periods)}.");
    }
}
