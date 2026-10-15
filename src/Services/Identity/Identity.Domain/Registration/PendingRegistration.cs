using Identity.Domain.Common;

namespace Identity.Domain.Registration;

/// <summary>
/// "Join the squad" before the email code is entered. The account is created only once the email is verified,
/// so an unverified address never blocks the real owner from signing up.
/// </summary>
public sealed class PendingRegistration : Entity
{
    private PendingRegistration()
    {
    }

    public string Email { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>Privacy-policy version the learner ticked on the sign-up form.</summary>
    public string ConsentPolicyVersion { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static PendingRegistration Start(
        string tenantId,
        string email,
        string firstName,
        string lastName,
        string passwordHash,
        string consentPolicyVersion,
        DateTime now) => new()
        {
            TenantId = tenantId,
            Email = email,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PasswordHash = passwordHash,
            ConsentPolicyVersion = consentPolicyVersion,
            CreatedAt = now,
            UpdatedAt = now,
        };

    /// <summary>Signing up again before verifying replaces the details.</summary>
    public void Update(string firstName, string lastName, string passwordHash, string consentPolicyVersion, DateTime now)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PasswordHash = passwordHash;
        ConsentPolicyVersion = consentPolicyVersion;
        UpdatedAt = now;
    }
}
