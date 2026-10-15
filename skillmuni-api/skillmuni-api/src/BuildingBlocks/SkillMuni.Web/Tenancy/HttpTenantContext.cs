using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Tenancy;
using SkillMuni.Web.Auth;

namespace SkillMuni.Web.Tenancy;

/// <summary>
/// Tenant set by background work, else from the access token when signed in, else from the gateway's header,
/// else the default.
/// </summary>
public sealed class HttpTenantContext(IHttpContextAccessor accessor, IOptions<TenancyOptions> options, TenantOverride tenantOverride) : ITenantContext
{
    public string TenantId
    {
        get
        {
            if (tenantOverride.TenantId is { } fromBackgroundWork)
            {
                return fromBackgroundWork;
            }

            var http = accessor.HttpContext;
            var fromToken = http?.User.TenantId();
            if (!string.IsNullOrEmpty(fromToken))
            {
                return fromToken;
            }

            var fromHeader = http?.Request.Headers[TenancyOptions.TenantHeader].ToString();
            return string.IsNullOrEmpty(fromHeader) ? options.Value.DefaultTenant : fromHeader;
        }
    }

    public string Market =>
        options.Value.Tenants.TryGetValue(TenantId, out var settings) ? settings.Market : "IN";
}
