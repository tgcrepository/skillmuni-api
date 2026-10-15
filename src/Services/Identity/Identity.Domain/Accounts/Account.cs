using Identity.Domain.Common;

namespace Identity.Domain.Accounts;

public enum AccountStatus
{
    Active = 0,
    Locked = 1,
    Erased = 2,
}

/// <summary>
/// A person who can sign in. Identity owns logins only; the learner profile (quests, photo, skills) lives in
/// the Learner service, keyed by the same id.
/// </summary>
public sealed class Account : Entity
{
    public const string LearnerRole = "learner";

    private Account()
    {
    }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    /// <summary>Lower-cased. Set only once verified (registration code, Google, or a contact change).</summary>
    public string? Email { get; private set; }

    /// <summary>E.164. Set only once verified with a code.</summary>
    public string? Phone { get; private set; }

    public string? PasswordHash { get; private set; }

    public string? GoogleSubject { get; private set; }

    /// <summary>Locked is an administrative state (423); wrong passwords never lock an account (B-46).</summary>
    public AccountStatus Status { get; private set; }

    /// <summary>The Learner profile photo (<c>learner.photo.changed</c>), for <c>AuthUser.photoUrl</c> (A-277).</summary>
    public string? PhotoUrl { get; private set; }

    public string Market { get; private set; } = "IN";

    /// <summary>Comma-separated roles: learner, institute-admin, tenant-admin.</summary>
    public string Roles { get; private set; } = LearnerRole;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public DateTime? LastLoginAt { get; private set; }

    public IReadOnlyList<string> RoleList =>
        Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool IsErased => Status == AccountStatus.Erased;

    public static Account RegisterWithEmail(
        string tenantId,
        string market,
        string firstName,
        string lastName,
        string email,
        string passwordHash,
        DateTime now) =>
        New(tenantId, market, firstName, lastName, now, a =>
        {
            a.Email = email;
            a.PasswordHash = passwordHash;
        });

    public static Account RegisterWithPhone(string tenantId, string market, string firstName, string lastName, string phone, DateTime now) =>
        New(tenantId, market, firstName, lastName, now, a => a.Phone = phone);

    public static Account RegisterWithGoogle(
        string tenantId,
        string market,
        string firstName,
        string lastName,
        string email,
        string googleSubject,
        DateTime now) =>
        New(tenantId, market, firstName, lastName, now, a =>
        {
            a.Email = email;
            a.GoogleSubject = googleSubject;
        });

    public bool IsLocked(DateTime now) => Status == AccountStatus.Locked;

    public void GrantRole(string role, DateTime now)
    {
        if (!RoleList.Contains(role, StringComparer.Ordinal))
        {
            Roles = string.Join(',', RoleList.Append(role));
            UpdatedAt = now;
        }
    }

    public void Lock(DateTime now)
    {
        Status = AccountStatus.Locked;
        UpdatedAt = now;
    }

    public void RecordLogin(DateTime now)
    {
        LastLoginAt = now;
        UpdatedAt = now;
    }

    public void SetPhoto(string? photoUrl, DateTime now)
    {
        PhotoUrl = string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl;
        UpdatedAt = now;
    }

    public void LinkGoogle(string subject, DateTime now)
    {
        GoogleSubject = subject;
        UpdatedAt = now;
    }

    public void ChangePassword(string passwordHash, DateTime now)
    {
        PasswordHash = passwordHash;
        UpdatedAt = now;
    }

    public void ChangePhone(string phone, DateTime now)
    {
        Phone = phone;
        UpdatedAt = now;
    }

    public void ChangeEmail(string email, DateTime now)
    {
        Email = email;
        UpdatedAt = now;
    }

    /// <summary>DPDP / POPIA erasure: the row stays (other services hold its id) but every personal detail goes.</summary>
    public void Erase(DateTime now)
    {
        Status = AccountStatus.Erased;
        FirstName = string.Empty;
        LastName = string.Empty;
        Email = null;
        Phone = null;
        PasswordHash = null;
        GoogleSubject = null;
        PhotoUrl = null;
        UpdatedAt = now;
    }

    private static Account New(string tenantId, string market, string firstName, string lastName, DateTime now, Action<Account> login)
    {
        var account = new Account
        {
            TenantId = tenantId,
            Market = market,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Status = AccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        login(account);
        return account;
    }
}
