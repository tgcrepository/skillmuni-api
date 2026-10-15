using Microsoft.Extensions.Options;
using SkillMuni.Web.Tenancy;

namespace SkillMuni.Gateway;

/// <summary>
/// Maps the request host to a tenant and sends it downstream in <c>X-Tenant-Id</c>, overwriting anything the
/// client sent. Services trust this header because only the gateway can reach them.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, IOptions<TenancyOptions> options)
{
    public Task InvokeAsync(HttpContext context)
    {
        var settings = options.Value;
        var tenant = settings.Hosts.TryGetValue(context.Request.Host.Host, out var mapped) ? mapped : settings.DefaultTenant;
        context.Request.Headers[TenancyOptions.TenantHeader] = tenant;
        return next(context);
    }
}
