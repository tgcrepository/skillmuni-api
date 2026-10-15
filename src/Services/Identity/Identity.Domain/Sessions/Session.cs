using Identity.Domain.Common;

namespace Identity.Domain.Sessions;

public enum SessionState
{
    Active,
    Idle,
    Expired,
    Revoked,
}

/// <summary>
/// A signed-in device. The refresh token (stored hashed) rotates on every use; the previous one keeps working for
/// a few seconds so two tabs refreshing at once don't sign each other out.
/// </summary>
public sealed class Session : Entity
{
    private Session()
    {
    }

    public Guid AccountId { get; private set; }

    public string RefreshTokenHash { get; private set; } = string.Empty;

    public string? PreviousRefreshTokenHash { get; private set; }

    public DateTime? RotatedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime LastSeenAt { get; private set; }

    /// <summary>Absolute limit, whatever the activity.</summary>
    public DateTime ExpiresAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public string? UserAgent { get; private set; }

    public static Session Start(string tenantId, Guid accountId, string refreshTokenHash, DateTime now, TimeSpan maxAge, string? userAgent) => new()
    {
        TenantId = tenantId,
        AccountId = accountId,
        RefreshTokenHash = refreshTokenHash,
        CreatedAt = now,
        LastSeenAt = now,
        ExpiresAt = now + maxAge,
        UserAgent = userAgent is { Length: > 256 } ? userAgent[..256] : userAgent,
    };

    public SessionState Evaluate(DateTime now, TimeSpan idleTimeout)
    {
        if (RevokedAt is not null)
        {
            return SessionState.Revoked;
        }

        if (now >= ExpiresAt)
        {
            return SessionState.Expired;
        }

        return now - LastSeenAt > idleTimeout ? SessionState.Idle : SessionState.Active;
    }

    /// <summary>True when <paramref name="hash"/> is the token just rotated out, still inside the grace window.</summary>
    public bool IsInGrace(string hash, DateTime now, TimeSpan grace) =>
        PreviousRefreshTokenHash == hash && RotatedAt is { } rotated && now - rotated <= grace;

    public void Touch(DateTime now) => LastSeenAt = now;

    public void Rotate(string newRefreshTokenHash, DateTime now)
    {
        PreviousRefreshTokenHash = RefreshTokenHash;
        RefreshTokenHash = newRefreshTokenHash;
        RotatedAt = now;
        LastSeenAt = now;
    }

    public void Revoke(DateTime now) => RevokedAt ??= now;
}
