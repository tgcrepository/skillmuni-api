using System.Text.Json.Serialization;
using FluentValidation;

namespace Platform.Application;

// Geo shapes match contact.api.ts; theme and market match the app's TenantTheme (core/theme/tenant-theme.ts) and
// MarketConfig (core/market/market.ts). Notifications (MISS-06) have no frontend type yet. All camelCase.

public sealed record PostalAreaDto(string Name, string Sub);

public sealed record PostalLookupDto(string Region, string District, string City, IReadOnlyList<PostalAreaDto> Areas);

public sealed record TenantThemeDto(
    string Id,
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Brand,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Accent,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LogoUrl);

public sealed record MobileRuleDto(int Digits, string Pattern, string Example);

public sealed record PostalRuleDto(string Label, string Pattern, string Example);

public sealed record IdTypeDto(
    string Id,
    string Label,
    string Pattern,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Check);

public sealed record MarketConfigDto(
    string Code,
    string Name,
    string Locale,
    string DialCode,
    string TimeZone,
    MobileRuleDto Mobile,
    PostalRuleDto Postal,
    string ConsentLaw,
    IReadOnlyList<IdTypeDto> IdTypes,
    string IdVerification,
    IReadOnlyList<string> Languages,
    bool AsksRace,
    IReadOnlyList<string> IdHasBirthday);

public sealed record NotificationDto(string Id, string Kind, string Title, string Body, string? Link, bool Read, DateTime CreatedAt);

/// <summary>The standard page plus <c>unread</c> for the bell badge.</summary>
public sealed record NotificationPageDto(IReadOnlyList<NotificationDto> Items, int Page, int PageSize, int Total, int Unread);

public sealed record NotificationPreferencesDto(bool Push, bool WhatsApp, bool Sms, bool Email);

public sealed record UpdatePreferencesRequest(bool? Push, bool? WhatsApp, bool? Sms, bool? Email);

public sealed class UpdatePreferencesRequestValidator : AbstractValidator<UpdatePreferencesRequest>
{
    public UpdatePreferencesRequestValidator()
    {
        RuleFor(r => r.Push).NotNull();
        RuleFor(r => r.WhatsApp).NotNull();
        RuleFor(r => r.Sms).NotNull();
        RuleFor(r => r.Email).NotNull();
    }
}
