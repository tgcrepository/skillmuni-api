namespace SkillMuni.Web.Auth;

/// <summary>
/// Token validation settings shared by the gateway and every service. Only the public key lives here;
/// the private key belongs to the Identity service alone. PEM values may be given raw or base64-encoded.
/// </summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    public string Issuer { get; set; } = "skillmuni-identity";

    public string Audience { get; set; } = "skillmuni-api";

    public string SigningPublicKeyPem { get; set; } = string.Empty;

    public string AccessCookieName { get; set; } = "sm_at";

    public string RefreshCookieName { get; set; } = "sm_rt";
}

/// <summary>Authorization policies and the roles behind them (CLAUDE.md §6).</summary>
public static class Policies
{
    public const string Learner = "learner";
    public const string InstituteAdmin = "institute-admin";
    public const string TenantAdmin = "tenant-admin";
}
