namespace SkillMuni.Common.Tenancy;

/// <summary>
/// Scoped: set by background work (event consumers) that has no HTTP request, so the tenant query filters
/// still apply. The HTTP tenant context checks it first.
/// </summary>
public sealed class TenantOverride
{
    public string? TenantId { get; private set; }

    public void Set(string tenantId) => TenantId = tenantId;
}
