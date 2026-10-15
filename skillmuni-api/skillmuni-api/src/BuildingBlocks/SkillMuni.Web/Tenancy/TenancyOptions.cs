namespace SkillMuni.Web.Tenancy;

/// <summary>
/// The white-label tenants this deployment serves. The gateway maps the request host to a tenant and sends
/// it in <c>X-Tenant-Id</c>; services trust that header only because they are reachable from the gateway alone.
/// </summary>
public sealed class TenancyOptions
{
    public const string Section = "Tenancy";
    public const string TenantHeader = "X-Tenant-Id";

    public string DefaultTenant { get; set; } = "skillmuni-in";

    public Dictionary<string, TenantSettings> Tenants { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gateway only: request host → tenant id.</summary>
    public Dictionary<string, string> Hosts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TenantSettings
{
    /// <summary><c>IN</c> or <c>ZA</c> (approved decision 3: South Africa is configuration only).</summary>
    public string Market { get; set; } = "IN";

    /// <summary>Google Identity Services client id for this tenant (A-253). Empty = Google sign-in off.</summary>
    public string? GoogleClientId { get; set; }
}
