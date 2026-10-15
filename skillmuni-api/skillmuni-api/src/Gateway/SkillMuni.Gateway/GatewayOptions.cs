namespace SkillMuni.Gateway;

public sealed class GatewayOptions
{
    public const string Section = "Gateway";

    /// <summary>Frontend origins allowed to call the API with cookies.</summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>Requests per minute per client IP, all routes.</summary>
    public int GlobalPerMinute { get; set; } = 300;

    /// <summary>Requests per minute per client IP on login / sign-up / claim routes.</summary>
    public int AuthPerMinute { get; set; } = 20;

    /// <summary>
    /// The gateway swaps the refresh cookie for a new access token once the current one is this old. Each swap
    /// records activity, so the 30-minute idle timeout is accurate to this interval.
    /// </summary>
    public TimeSpan RefreshWhenOlderThan { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Internal address of the Identity service (Container Apps internal ingress).</summary>
    public Uri IdentityBaseAddress { get; set; } = new("http://identity:8080/");
}
