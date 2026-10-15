using Identity.Domain.Common;

namespace Identity.Domain.Codes;

/// <summary>What a code (or reset token) proves.</summary>
public enum CodePurpose
{
    LoginPhone = 0,
    LoginEmail = 1,
    RegisterEmail = 2,
    ContactPhone = 3,
    ContactEmail = 4,
    Claim = 5,
    PasswordReset = 6,
}

public enum CodeCheck
{
    Valid,
    Wrong,
    Expired,
    Exhausted,
}

/// <summary>
/// A one-time code (BR-136): 6 digits, a hidden limit on wrong tries (BR-11: 20), a fixed lifetime, and a resend
/// window of 30 seconds.
/// Only a keyed hash of the code is stored.
/// </summary>
public sealed class VerificationCode : Entity
{
    private VerificationCode()
    {
    }

    public CodePurpose Purpose { get; private set; }

    /// <summary>The normalised mobile (E.164) or email the code went to, or <c>claim:&lt;id&gt;</c>.</summary>
    public string Target { get; private set; } = string.Empty;

    public string CodeHash { get; private set; } = string.Empty;

    public Guid? AccountId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime ResendAt { get; private set; }

    public int TriesLeft { get; private set; }

    public DateTime? ConsumedAt { get; private set; }

    public static VerificationCode Issue(
        string tenantId,
        CodePurpose purpose,
        string target,
        string codeHash,
        Guid? accountId,
        DateTime now,
        TimeSpan lifetime,
        TimeSpan resendAfter,
        int tries) => new()
        {
            TenantId = tenantId,
            Purpose = purpose,
            Target = target,
            CodeHash = codeHash,
            AccountId = accountId,
            CreatedAt = now,
            ExpiresAt = now + lifetime,
            ResendAt = now + resendAfter,
            TriesLeft = tries,
        };

    /// <summary>Used successfully: its flow is over, so it no longer holds back a new code.</summary>
    public bool IsConsumed => ConsumedAt is not null;

    /// <summary>Not used, not expired and tries remain.</summary>
    public bool IsUsable(DateTime now) => ConsumedAt is null && now < ExpiresAt && TriesLeft > 0;

    public bool CanResend(DateTime now) => now >= ResendAt;

    public int SecondsUntilResend(DateTime now) => Math.Max(0, (int)Math.Ceiling((ResendAt - now).TotalSeconds));

    public int ValidForMinutes(DateTime now) => Math.Max(0, (int)Math.Ceiling((ExpiresAt - now).TotalMinutes));

    /// <summary>A newer code replaces this one: it stops working at once.</summary>
    public void Supersede(DateTime now)
    {
        if (ExpiresAt > now)
        {
            ExpiresAt = now;
        }
    }

    /// <summary>Checks an attempt. A wrong attempt uses up a try; the last allowed wrong one exhausts the code.</summary>
    public CodeCheck Check(bool matches, DateTime now)
    {
        if (ConsumedAt is not null || now >= ExpiresAt)
        {
            return CodeCheck.Expired;
        }

        if (TriesLeft <= 0)
        {
            return CodeCheck.Exhausted;
        }

        if (matches)
        {
            ConsumedAt = now;
            return CodeCheck.Valid;
        }

        TriesLeft--;
        return TriesLeft <= 0 ? CodeCheck.Exhausted : CodeCheck.Wrong;
    }
}
