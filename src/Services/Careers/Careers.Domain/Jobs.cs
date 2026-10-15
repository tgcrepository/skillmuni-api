using System.Text.Json;

namespace Careers.Domain;

/// <summary>A bookmarked catalogue job (FR-41). Unbookmarking deletes the row.</summary>
public sealed class SavedJob : LearnerOwned
{
    private SavedJob()
    {
    }

    public string JobId { get; private set; } = string.Empty;

    public DateTime SavedAt { get; private set; }

    public static SavedJob Save(string tenantId, Guid learnerId, string jobId, DateTime now) =>
        new() { TenantId = tenantId, LearnerId = learnerId, JobId = jobId, SavedAt = now };
}

/// <summary>A CV file the learner uploaded in the apply dialog (FR-42). The file itself is in Blob Storage.</summary>
public sealed class CvFile : LearnerOwned
{
    private CvFile()
    {
    }

    /// <summary>The learner's own file name (PII: it often carries their name).</summary>
    public string Name { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string BlobName { get; private set; } = string.Empty;

    public DateTime UploadedAt { get; private set; }

    public static CvFile Upload(string tenantId, Guid learnerId, string name, string contentType, long sizeBytes, DateTime now)
    {
        var file = new CvFile { TenantId = tenantId, LearnerId = learnerId, Name = name, ContentType = contentType, SizeBytes = sizeBytes, UploadedAt = now };
        file.BlobName = $"{tenantId}/{learnerId:N}/cv/{file.Id:N}";
        return file;
    }
}

public static class ApplicationStatus
{
    public const string Applied = "applied";
    public const string Viewed = "viewed";
    public const string Shortlisted = "shortlisted";
    public const string Interview = "interview";
    public const string Offer = "offer";
    public const string NotSelected = "not-selected";
    public const string Withdrawn = "withdrawn";

    public static readonly IReadOnlyList<string> All = [Applied, Viewed, Shortlisted, Interview, Offer, NotSelected, Withdrawn];
}

public static class ApplicationSource
{
    /// <summary>Sent by the learner from My Jobs.</summary>
    public const string Apply = "apply";

    /// <summary>An offer the learner marked after a Hire Fest interview (BR-70).</summary>
    public const string FestOffer = "fest-offer";
}

public sealed record ApplicationAnswer(string Q, string A);

/// <summary>
/// A job application. Employers move the status (viewed → shortlisted → interview → offer) once the employer side
/// exists; in Phase 1 the learner sends and withdraws, and a Hire Fest offer arrives as an <c>offer</c>.
/// </summary>
public sealed class JobApplication : LearnerOwned
{
    private JobApplication()
    {
    }

    public string JobId { get; private set; } = string.Empty;

    /// <summary>Shown instead of the catalogue title (a Hire Fest offer carries the employer's role).</summary>
    public string? Title { get; private set; }

    public Guid? CvId { get; private set; }

    public string CvName { get; private set; } = string.Empty;

    public string AnswersJson { get; private set; } = "[]";

    public string Status { get; private set; } = ApplicationStatus.Applied;

    public string Source { get; private set; } = ApplicationSource.Apply;

    public Guid? InvitationId { get; private set; }

    /// <summary><c>festId:employerId</c> for a Hire Fest offer, so marking it twice adds one application.</summary>
    public string? FestOfferKey { get; private set; }

    public DateTime AppliedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public DateTime? WithdrawnAt { get; private set; }

    public IReadOnlyList<ApplicationAnswer> Answers => JsonSerializer.Deserialize<List<ApplicationAnswer>>(AnswersJson) ?? [];

    public bool Active => Status != ApplicationStatus.Withdrawn;

    public static JobApplication Send(
        string tenantId,
        Guid learnerId,
        string jobId,
        Guid cvId,
        string cvName,
        IReadOnlyList<ApplicationAnswer> answers,
        Guid? invitationId,
        DateTime now) => new()
        {
            TenantId = tenantId,
            LearnerId = learnerId,
            JobId = jobId,
            CvId = cvId,
            CvName = cvName,
            AnswersJson = JsonSerializer.Serialize(answers),
            InvitationId = invitationId,
            AppliedAt = now,
            UpdatedAt = now,
        };

    public static JobApplication FestOffer(string tenantId, Guid learnerId, string jobId, string title, string festOfferKey, string cvName, DateTime interviewedAt, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        JobId = jobId,
        Title = title,
        CvName = cvName,
        Status = ApplicationStatus.Offer,
        Source = ApplicationSource.FestOffer,
        FestOfferKey = festOfferKey,
        AppliedAt = interviewedAt,
        UpdatedAt = now,
    };

    /// <summary>BR-66: allowed until an offer. Withdrawing twice changes nothing.</summary>
    /// <returns>False when the application can no longer be withdrawn.</returns>
    public bool Withdraw(DateTime now)
    {
        if (Status == ApplicationStatus.Offer)
        {
            return false;
        }

        if (Status != ApplicationStatus.Withdrawn)
        {
            Status = ApplicationStatus.Withdrawn;
            WithdrawnAt = now;
            UpdatedAt = now;
        }

        return true;
    }
}

public static class InvitationStatus
{
    public const string New = "new";
    public const string Accepted = "accepted";
    public const string Declined = "declined";
    public const string Expired = "expired";
}

/// <summary>
/// A recruiter's invitation to apply or to interview. Created by the employer side (not built in Phase 1); expires
/// after <see cref="RespondBy"/> (7 days by default, BR-66).
/// </summary>
public sealed class Invitation : LearnerOwned
{
    private Invitation()
    {
    }

    public string JobId { get; private set; } = string.Empty;

    public string RecruiterName { get; private set; } = string.Empty;

    public string RecruiterInitials { get; private set; } = string.Empty;

    public string RecruiterTitle { get; private set; } = string.Empty;

    /// <summary><c>apply</c> or <c>interview</c>.</summary>
    public string Type { get; private set; } = "apply";

    public DateTime? InterviewAt { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public DateOnly RespondBy { get; private set; }

    /// <summary>new · accepted · declined (expired is derived from <see cref="RespondBy"/>).</summary>
    public string Status { get; private set; } = InvitationStatus.New;

    /// <summary>The learner's note with a decline or a "suggest a time".</summary>
    public string? Note { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? RespondedAt { get; private set; }

    public static Invitation Create(
        string tenantId,
        Guid learnerId,
        string jobId,
        string recruiterName,
        string recruiterInitials,
        string recruiterTitle,
        string type,
        DateTime? interviewAt,
        string message,
        DateOnly respondBy,
        DateTime now) => new()
        {
            TenantId = tenantId,
            LearnerId = learnerId,
            JobId = jobId,
            RecruiterName = recruiterName,
            RecruiterInitials = recruiterInitials,
            RecruiterTitle = recruiterTitle,
            Type = type,
            InterviewAt = interviewAt,
            Message = message,
            RespondBy = respondBy,
            CreatedAt = now,
        };

    public string StatusOn(DateOnly today) => Status == InvitationStatus.New && today > RespondBy ? InvitationStatus.Expired : Status;

    public bool IsExpired(DateOnly today) => StatusOn(today) == InvitationStatus.Expired;

    public void Accept(string? note, DateTime now) => Respond(InvitationStatus.Accepted, note, now);

    public void Decline(string? note, DateTime now) => Respond(InvitationStatus.Declined, note, now);

    /// <summary>"Suggest another time": the invitation stays open with the learner's note for the recruiter.</summary>
    public void Suggest(string? note, DateTime now)
    {
        Note = string.IsNullOrWhiteSpace(note) ? Note : note.Trim();
        RespondedAt = now;
    }

    private void Respond(string status, string? note, DateTime now)
    {
        Status = status;
        Note = string.IsNullOrWhiteSpace(note) ? Note : note.Trim();
        RespondedAt = now;
    }
}
