namespace Platform.Domain;

/// <summary>
/// A white-label tenant's look (MISS-12, A-13): name, logo and overrides for the brand ("ember") and accent ("arcane")
/// colour ramps, as JSON objects of ramp step → hex colour. One row per tenant.
/// </summary>
public sealed class TenantTheme : Entity
{
    public const int MaxNameLength = 80;
    public const int MaxLogoUrlLength = 500;

    private TenantTheme()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? LogoUrl { get; private set; }

    /// <summary>JSON object, e.g. <c>{"500":"#ff6a00"}</c>. <c>{}</c> = Skillmuni's own ramp.</summary>
    public string BrandJson { get; private set; } = "{}";

    public string AccentJson { get; private set; } = "{}";

    public DateTime UpdatedAt { get; private set; }

    public static TenantTheme Create(string tenantId, string name, string? logoUrl, string brandJson, string accentJson, DateTime now) => new()
    {
        TenantId = tenantId,
        Name = name,
        LogoUrl = logoUrl,
        BrandJson = brandJson,
        AccentJson = accentJson,
        UpdatedAt = now,
    };
}
