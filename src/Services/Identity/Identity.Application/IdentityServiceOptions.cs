namespace Identity.Application;

/// <summary>Identity rules from BR-136 and the session decisions. Bound from the <c>Identity</c> config section.</summary>
public sealed class IdentityServiceOptions
{
    public const string Section = "Identity";

    /// <summary>Wrong attempts allowed per code before 429 too-many-tries.</summary>
    public int TriesPerCode { get; set; } = 5;

    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>"Resend" unlocks after this long.</summary>
    public TimeSpan ResendAfter { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Per-target cap on codes sent, so one number or inbox can't be flooded.</summary>
    public int MaxCodesPerHour { get; set; } = 5;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Signed out after this long without activity.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Signed out after this long whatever the activity.</summary>
    public TimeSpan SessionMaxAge { get; set; } = TimeSpan.FromDays(30);

    /// <summary>A just-rotated refresh token still works this long (two tabs refreshing at once).</summary>
    public TimeSpan RefreshGrace { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxFailedPasswords { get; set; } = 10;

    public TimeSpan PasswordLockout { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan ResetLinkLifetime { get; set; } = TimeSpan.FromMinutes(30);

    public TimeSpan ResetResendAfter { get; set; } = TimeSpan.FromSeconds(45);

    /// <summary>Privacy-policy version recorded with each consent.</summary>
    public string PrivacyPolicyVersion { get; set; } = "2026-10";
}
