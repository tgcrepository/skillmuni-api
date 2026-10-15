namespace SkillMuni.Gateway;

public sealed class GatewayOptions
{
    public const string Section = "Gateway";

    /// <summary>Frontend origins allowed to call the API with cookies.</summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Requests per minute per client IP, all routes. Sized for a college lab or a mobile carrier sharing one IP: a
    /// screen makes several calls, so 300 blocked about ten learners browsing together.
    /// </summary>
    public int GlobalPerMinute { get; set; } = 1200;

    /// <summary>
    /// Requests per minute per client IP on login / sign-up / claim routes. Codes have their own limits (20 wrong
    /// tries per code, 5 codes an hour per mobile or email), so this only stops floods.
    /// </summary>
    public int AuthPerMinute { get; set; } = 120;

    /// <summary>
    /// The gateway swaps the refresh cookie for a new access token once the current one is this old. Each swap
    /// records activity, so the 30-minute idle timeout is accurate to this interval.
    /// </summary>
    public TimeSpan RefreshWhenOlderThan { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Internal address of the Identity service (Container Apps internal ingress).</summary>
    public Uri IdentityBaseAddress { get; set; } = new("http://identity:8080/");
}
