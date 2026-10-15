namespace SkillMuni.Common.Tenancy;

/// <summary>The tenant (white-label client) and market the current request belongs to.</summary>
public interface ITenantContext
{
    string TenantId { get; }

    /// <summary>Market code, <c>IN</c> or <c>ZA</c>.</summary>
    string Market { get; }
}

/// <summary>The correlation id of the current request, passed on to events and outgoing calls.</summary>
public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
