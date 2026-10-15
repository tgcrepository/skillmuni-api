namespace Learner.Domain;

/// <summary>
/// What the Job Profile keeps beyond the quest answers it is built from (A-262): the learner's own "About me", the
/// share-link settings, the latest CV read (for "Upload a newer CV", P5) and which suggested changes were applied.
/// One row per learner, created on first use.
/// </summary>
public sealed class JobProfileSettings : Entity
{
    public const int MaxAboutLength = 400;
    public const int DefaultExpiresDays = 30;

    private JobProfileSettings()
    {
    }

    public Guid LearnerId { get; private set; }

    /// <summary>The learner's rewrite (FR-13); null = Muni's summary built from the quests.</summary>
    public string? About { get; private set; }

    /// <summary>A-208: the shared link hides phone and email by default.</summary>
    public bool HideContact { get; private set; } = true;

    public int ExpiresDays { get; private set; } = DefaultExpiresDays;

    /// <summary>Random, unguessable code in the share link and the CV footer (<c>skillmuni.app/v/&lt;code&gt;</c>).</summary>
    public string ShareCode { get; private set; } = string.Empty;

    /// <summary>Quest slug → answers read from the learner's latest CV (<c>cv.parse.completed</c>), as JSON.</summary>
    public string CvPrefillJson { get; private set; } = "{}";

    public Guid? CvId { get; private set; }

    /// <summary>The newer CV uploaded from the Job Profile, kept in Blob Storage.</summary>
    public string? CvBlob { get; private set; }

    /// <summary>Ids of suggested CV changes the learner accepted, as a JSON array.</summary>
    public string AppliedChangesJson { get; private set; } = "[]";

    public DateTime UpdatedAt { get; private set; }

    public static JobProfileSettings Create(string tenantId, Guid learnerId, string shareCode, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        ShareCode = shareCode,
        UpdatedAt = now,
    };

    /// <summary>Empty text goes back to Muni's summary.</summary>
    public void RewriteAbout(string? text, DateTime now)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length > MaxAboutLength)
        {
            throw new ArgumentException($"About me is at most {MaxAboutLength} characters.", nameof(text));
        }

        About = trimmed.Length == 0 ? null : trimmed;
        UpdatedAt = now;
    }

    public void SetSharing(bool hideContact, int expiresDays, DateTime now)
    {
        HideContact = hideContact;
        ExpiresDays = expiresDays;
        UpdatedAt = now;
    }

    public void KeepCvRead(Guid cvId, string prefillJson, DateTime now)
    {
        CvId = cvId;
        CvPrefillJson = prefillJson;
        UpdatedAt = now;
    }

    /// <returns>The blob of the CV this one replaces, to delete.</returns>
    public string? ReplaceCv(string blobName, DateTime now)
    {
        var previous = CvBlob;
        CvBlob = blobName;
        UpdatedAt = now;
        return previous;
    }

    public void RecordApplied(string appliedChangesJson, DateTime now)
    {
        AppliedChangesJson = appliedChangesJson;
        UpdatedAt = now;
    }
}
