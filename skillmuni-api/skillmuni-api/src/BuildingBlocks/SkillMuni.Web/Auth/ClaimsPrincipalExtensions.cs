using System.Security.Claims;
using SkillMuni.Common.Security;

namespace SkillMuni.Web.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid? AccountId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(SkillMuniClaims.Subject)?.Value, out var id) ? id : null;

    public static Guid? SessionId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(SkillMuniClaims.Session)?.Value, out var id) ? id : null;

    public static string? TenantId(this ClaimsPrincipal user) => user.FindFirst(SkillMuniClaims.Tenant)?.Value;
}
