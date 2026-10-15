using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;

namespace Platform.Application.Services;

/// <summary>MISS-12: the tenant's theme and the market config, so neither is client config any more.</summary>
public interface ITenantConfigService
{
    Task<TenantThemeDto> ThemeAsync(CancellationToken cancellationToken);

    /// <param name="code">A market to read instead of the tenant's (the Country step can switch it, A-23).</param>
    Result<MarketConfigDto> Market(string? code);
}

public sealed partial class TenantConfigService(ITenantThemeRepository themes, ITenantContext tenant, IOptions<PlatformOptions> options) : ITenantConfigService
{
    /// <summary>Ramp steps the app lets a tenant override (tenant-theme.ts BRAND_STEPS / ACCENT_STEPS).</summary>
    internal static readonly HashSet<string> BrandSteps = ["50", "100", "150", "200", "300", "350", "400", "450", "500", "550", "600", "700", "800", "900"];

    internal static readonly HashSet<string> AccentSteps = ["50", "100", "200", "300", "350", "400", "450", "500", "550", "600", "700", "800", "900"];

    public async Task<TenantThemeDto> ThemeAsync(CancellationToken cancellationToken)
    {
        var id = tenant.TenantId;
        var theme = await themes.FindAsync(id, cancellationToken);
        if (theme is null)
        {
            return new TenantThemeDto(id, options.Value.DefaultThemeName, null, null, null);
        }

        return new TenantThemeDto(
            id,
            theme.Name,
            Ramp(theme.BrandJson, BrandSteps),
            Ramp(theme.AccentJson, AccentSteps),
            string.IsNullOrWhiteSpace(theme.LogoUrl) ? null : theme.LogoUrl);
    }

    public Result<MarketConfigDto> Market(string? code)
    {
        var key = string.IsNullOrWhiteSpace(code) ? tenant.Market : code.Trim();
        if (!options.Value.AllMarkets().TryGetValue(key, out var market))
        {
            return PlatformErrors.UnknownMarket;
        }

        return new MarketConfigDto(
            market.Code,
            market.Name,
            market.Locale,
            market.DialCode,
            market.TimeZone,
            new MobileRuleDto(market.Mobile.Digits, market.Mobile.Pattern, market.Mobile.Example),
            new PostalRuleDto(market.Postal.Label, market.Postal.Pattern, market.Postal.Example),
            market.ConsentLaw,
            market.IdTypes.Select(t => new IdTypeDto(t.Id, t.Label, t.Pattern, t.Check)).ToList(),
            market.IdVerification,
            market.Languages,
            market.AsksRace,
            market.IdHasBirthday);
    }

    /// <summary>Only known steps with valid hex colours, as the app would apply them; none → null (keep Skillmuni's).</summary>
    internal static IReadOnlyDictionary<string, string>? Ramp(string json, HashSet<string> steps)
    {
        Dictionary<string, string>? values;
        try
        {
            values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }

        var valid = (values ?? new Dictionary<string, string>())
            .Where(kv => steps.Contains(kv.Key) && Hex().IsMatch(kv.Value))
            .OrderBy(kv => int.Parse(kv.Key, CultureInfo.InvariantCulture))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        return valid.Count == 0 ? null : valid;
    }

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.CultureInvariant)]
    private static partial Regex Hex();
}
