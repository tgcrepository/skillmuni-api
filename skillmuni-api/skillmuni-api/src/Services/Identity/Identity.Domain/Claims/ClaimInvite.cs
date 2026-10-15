using Identity.Domain.Common;

namespace Identity.Domain.Claims;

public enum ClaimStatus
{
    Open = 0,
    Claimed = 1,
    Declined = 2,
}

public enum ClaimStage
{
    Invited = 0,
    Verified = 1,
    Consented = 2,
    Finished = 3,
}

/// <summary>
/// An institute uploaded a CV for a learner and sent them a link (Figma section 11, V1–V5). The learner proves
/// the mobile with a code, consents, checks each section and confirms, or declines and everything is deleted.
/// Only a hash of the link token is stored.
/// </summary>
public sealed class ClaimInvite : Entity
{
    private ClaimInvite()
    {
    }

    public string TokenHash { get; private set; } = string.Empty;

    public string Institute { get; private set; } = string.Empty;

    /// <summary>Name as the institute entered it.</summary>
    public string LearnerName { get; private set; } = string.Empty;

    public string Programme { get; private set; } = string.Empty;

    public string Batch { get; private set; } = string.Empty;

    /// <summary>"Contact, education, 8 skills, 2 languages, 1 job".</summary>
    public string FromCv { get; private set; } = string.Empty;

    /// <summary>E.164 mobile the code goes to.</summary>
    public string Phone { get; private set; } = string.Empty;

    /// <summary>"NSQF L4 certificate".</summary>
    public string Certificate { get; private set; } = string.Empty;

    /// <summary>The CV is deleted on this date if nobody claims it.</summary>
    public DateTime DeleteBy { get; private set; }

    public ClaimStatus Status { get; private set; }

    public ClaimStage Stage { get; private set; }

    public Guid? AccountId { get; private set; }

    /// <summary>The V3 sections as JSON (read from the CV; the Application layer owns the shape).</summary>
    public string SectionsJson { get; private set; } = "[]";

    /// <summary>The V4 numbers as JSON, or null until known.</summary>
    public string? ResultJson { get; private set; }

    /// <summary>Comma-separated slugs the learner tapped "Looks right" on.</summary>
    public string ConfirmedSlugs { get; private set; } = string.Empty;

    public bool? ShareProgress { get; private set; }

    public bool? VerifyCertificate { get; private set; }

    public bool? ApplyForMe { get; private set; }

    public string? DeclineReason { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<string> ConfirmedSlugList =>
        ConfirmedSlugs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The verification code's target: one code stream per invite.</summary>
    public string CodeTarget => $"claim:{Id:N}";

    public static ClaimInvite Create(
        string tenantId,
        string tokenHash,
        string institute,
        string learnerName,
        string programme,
        string batch,
        string fromCv,
        string phone,
        string certificate,
        DateTime deleteBy,
        string sectionsJson,
        string? resultJson,
        DateTime now) => new()
        {
            TenantId = tenantId,
            TokenHash = tokenHash,
            Institute = institute,
            LearnerName = learnerName,
            Programme = programme,
            Batch = batch,
            FromCv = fromCv,
            Phone = phone,
            Certificate = certificate,
            DeleteBy = deleteBy,
            SectionsJson = sectionsJson,
            ResultJson = resultJson,
            Status = ClaimStatus.Open,
            Stage = ClaimStage.Invited,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public bool IsExpired(DateTime now) => Status == ClaimStatus.Open && now >= DeleteBy;

    public bool IsClaimant(Guid? accountId) => accountId is not null && AccountId == accountId;

    public void MarkVerified(Guid accountId, DateTime now)
    {
        AccountId = accountId;
        if (Stage < ClaimStage.Verified)
        {
            Stage = ClaimStage.Verified;
        }

        UpdatedAt = now;
    }

    public void Consent(bool shareProgress, bool verifyCertificate, bool applyForMe, DateTime now)
    {
        ShareProgress = shareProgress;
        VerifyCertificate = verifyCertificate;
        ApplyForMe = applyForMe;
        if (Stage < ClaimStage.Consented)
        {
            Stage = ClaimStage.Consented;
        }

        UpdatedAt = now;
    }

    public bool IsConfirmed(string slug) => ConfirmedSlugList.Contains(slug, StringComparer.Ordinal);

    public void ConfirmSection(string slug, DateTime now)
    {
        if (!IsConfirmed(slug))
        {
            ConfirmedSlugs = string.Join(',', ConfirmedSlugList.Append(slug));
            UpdatedAt = now;
        }
    }

    public void Finish(DateTime now)
    {
        Status = ClaimStatus.Claimed;
        Stage = ClaimStage.Finished;
        UpdatedAt = now;
    }

    /// <summary>V5: the CV and everything read from it goes. The institute keeps only "Declined", not why.</summary>
    public void Decline(string? reason, DateTime now)
    {
        Status = ClaimStatus.Declined;
        DeclineReason = reason;
        LearnerName = string.Empty;
        Phone = string.Empty;
        FromCv = string.Empty;
        SectionsJson = "[]";
        ResultJson = null;
        ConfirmedSlugs = string.Empty;
        UpdatedAt = now;
    }
}
