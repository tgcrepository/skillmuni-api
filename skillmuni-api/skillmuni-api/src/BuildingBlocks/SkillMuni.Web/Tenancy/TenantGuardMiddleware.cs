using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Tenancy;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace SkillMuni.Web.Tenancy;

/// <summary>
/// Rejects a tenant this deployment doesn't serve, and a token from one tenant used on another tenant's host
/// (the gateway sends the host's tenant in <c>X-Tenant-Id</c>).
/// </summary>
public sealed class TenantGuardMiddleware(RequestDelegate next, IOptions<TenancyOptions> options)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        var known = options.Value.Tenants;
        if (known.Count > 0 && !known.ContainsKey(tenant.TenantId))
        {
            var problem = ApiProblems.Create(context, StatusCodes.Status400BadRequest, "unknown-tenant", "Unknown tenant.");
            await ApiProblems.WriteAsync(context, problem, context.RequestAborted);
            return;
        }

        var fromHeader = context.Request.Headers[TenancyOptions.TenantHeader].ToString();
        var fromToken = context.User.TenantId();
        if (!string.IsNullOrEmpty(fromHeader) && !string.IsNullOrEmpty(fromToken)
            && !string.Equals(fromHeader, fromToken, StringComparison.OrdinalIgnoreCase))
        {
            var problem = ApiProblems.Create(context, StatusCodes.Status403Forbidden, "tenant-mismatch", "This session belongs to another app.");
            await ApiProblems.WriteAsync(context, problem, context.RequestAborted);
            return;
        }

        await next(context);
    }
}
