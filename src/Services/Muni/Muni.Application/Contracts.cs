using System.Text.Json.Serialization;
using FluentValidation;

namespace Muni.Application;

// Shapes match muni.api.ts (FRONTEND_TYPES.md), serialised camelCase.

/// <summary>The caller as the access token describes them.</summary>
public sealed record LearnerRef(Guid Id, string FirstName, string Market);

/// <summary>"For you right now" card. Exactly one of <see cref="Link"/> (app route) or <see cref="Intent"/> (ask Muni).</summary>
public sealed record MuniSuggestionDto(
    string Id,
    string Icon,
    string Tone,
    string Title,
    string Detail,
    string Cta,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Link = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Intent = null);

public sealed record MuniTopicDto(string Id, string Icon, string Label, int Options);

public sealed record MuniNudgeDto(string Title, string Body);

public sealed record MuniWelcomeDto(
    string FirstName,
    string Intro,
    IReadOnlyList<MuniSuggestionDto> Suggestions,
    IReadOnlyList<MuniTopicDto> Topics,
    int Unread,
    MuniNudgeDto? Nudge);

/// <summary>A reply bubble or card; <c>kind</c> tells the app which.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(ListBlock), "list")]
[JsonDerivedType(typeof(StatsBlock), "stats")]
[JsonDerivedType(typeof(ProfileBlock), "profile")]
public abstract record MuniBlock;

public sealed record TextBlock(string Text) : MuniBlock;

public sealed record ListItem(
    string Title,
    string Detail,
    string Url,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Tag = null);

public sealed record ListBlock(IReadOnlyList<ListItem> Items) : MuniBlock;

public sealed record StatItem(string Label, string Value, string Icon);

public sealed record LinkDto(string Label, string Url);

public sealed record StatsBlock(IReadOnlyList<StatItem> Items, LinkDto Link) : MuniBlock;

/// <summary><c>key</c>: email · mobile · since. Private values arrive masked; the eye calls <c>muni/reveal</c>.</summary>
public sealed record ProfileField(string Key, string Label, string Value, bool Masked);

public sealed record ProfileBlock(
    string Initials,
    string Name,
    string Subtitle,
    IReadOnlyList<ProfileField> Fields,
    int Completeness,
    string NextStep) : MuniBlock;

/// <summary>
/// muni.api.ts <c>MuniReply</c> plus <c>id</c> (additive): the stored reply id, which the app may send back to
/// <c>PUT muni/messages/{id}/rating</c>.
/// </summary>
public sealed record MuniReplyDto(string Id, IReadOnlyList<MuniBlock> Blocks, IReadOnlyList<string> Chips);

public sealed record HistoryLineDto(string From, string Text);

public sealed record MuniHistoryItemDto(string Id, string Title, string When, IReadOnlyList<HistoryLineDto> Messages);

public sealed record RevealDto(string Value);

/// <summary><c>intent</c> is a topic id or chip text; free text goes in <c>text</c>.</summary>
public sealed record AskRequest(string? Text, string? Intent);

public sealed record RevealRequest(string? Field);

/// <summary><c>helpful: null</c> clears the rating.</summary>
public sealed record RateRequest(bool? Helpful);

public static class RevealFields
{
    public const string Email = "email";
    public const string Mobile = "mobile";

    public static readonly string[] All = [Email, Mobile];
}

public sealed class AskRequestValidator : AbstractValidator<AskRequest>
{
    public const int MaxTextLength = 500;
    public const int MaxIntentLength = 100;

    public AskRequestValidator()
    {
        RuleFor(r => r.Text)
            .Must((request, text) => !string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(request.Intent))
            .WithMessage("Type a question or pick a chip.");
        RuleFor(r => r.Text).MaximumLength(MaxTextLength);
        RuleFor(r => r.Intent).MaximumLength(MaxIntentLength);
    }
}

public sealed class RevealRequestValidator : AbstractValidator<RevealRequest>
{
    public RevealRequestValidator() =>
        RuleFor(r => r.Field).NotEmpty().Must(f => f is not null && RevealFields.All.Contains(f, StringComparer.Ordinal)).WithMessage("Field is email or mobile.");
}
