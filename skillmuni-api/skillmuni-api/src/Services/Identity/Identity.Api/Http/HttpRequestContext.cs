using Identity.Application.Abstractions;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Tenancy;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Tenancy;

namespace Identity.Api.Http;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? AccountId => accessor.HttpContext?.User.AccountId();

    public Guid? SessionId => accessor.HttpContext?.User.SessionId();

    public string? UserAgent =>
        accessor.HttpContext?.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null;
}

public sealed class HttpTenantSettings(ITenantContext tenant, IOptions<TenancyOptions> options) : ITenantSettings
{
    public string? GoogleClientId =>
        options.Value.Tenants.TryGetValue(tenant.TenantId, out var settings) ? settings.GoogleClientId : null;
}
