using Identity.Domain.Common;

namespace Identity.Domain.Consents;

/// <summary>
/// Proof of consent under DPDP (IN) or POPIA (ZA), MISS-07. Append-only: a withdrawal is a new record with
/// <see cref="Granted"/> = false, so the full history stays auditable.
/// </summary>
public sealed class ConsentRecord : Entity
{
    public const string RegistrationScope = "registration";

    private ConsentRecord()
    {
    }

    public Guid AccountId { get; private set; }

    /// <summary>What was agreed to, e.g. <c>registration</c>, <c>claim.share-progress</c>.</summary>
    public string Scope { get; private set; } = string.Empty;

    public string PolicyVersion { get; private set; } = string.Empty;

    /// <summary><c>DPDP</c> or <c>POPIA</c>.</summary>
    public string Law { get; private set; } = string.Empty;

    public bool Granted { get; private set; }

    /// <summary>Where it was given: <c>sign-up</c>, <c>claim</c>, <c>settings</c>…</summary>
    public string Source { get; private set; } = string.Empty;

    public DateTime RecordedAt { get; private set; }

    public static ConsentRecord Record(
        string tenantId,
        Guid accountId,
        string scope,
        string policyVersion,
        string law,
        bool granted,
        string source,
        DateTime now) => new()
        {
            TenantId = tenantId,
            AccountId = accountId,
            Scope = scope,
            PolicyVersion = policyVersion,
            Law = law,
            Granted = granted,
            Source = source,
            RecordedAt = now,
        };
}
