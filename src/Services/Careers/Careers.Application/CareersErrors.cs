using SkillMuni.Common.Results;

namespace Careers.Application;

public static class CareersErrors
{
    // ---- My Jobs ----
    public static readonly Error JobNotFound = Error.NotFound("job-not-found", "Job not found");

    public static readonly Error JobClosed = Error.Conflict("job-closed", "This job is no longer taking applications");

    public static readonly Error InvalidTab = Error.NotFound("tab-not-found", "Tab is available, matching or bookmarked");

    public static readonly Error InvitationNotFound = Error.NotFound("invitation-not-found", "Invitation not found");

    public static readonly Error InvitationExpired = Error.Gone("invitation-expired", "This invitation has expired");

    public static readonly Error ApplicationNotFound = Error.NotFound("application-not-found", "Application not found");

    public static readonly Error AlreadyApplied = Error.Conflict("already-applied", "You've already applied to this job");

    public static readonly Error ReapplyCoolOff = Error.Conflict("reapply-cool-off", "You can apply to this job again 30 days after withdrawing");

    public static readonly Error CannotWithdraw = Error.Conflict("cannot-withdraw", "An offer can't be withdrawn");

    public static readonly Error CvNotFound = Error.Validation(
        "cv-not-found",
        "Pick one of your CVs",
        new Dictionary<string, string[]> { ["cvId"] = ["Pick one of your CVs."] });

    public static readonly Error FileMissing = Error.Validation(
        "file-missing",
        "Choose a file",
        new Dictionary<string, string[]> { ["file"] = ["Choose a file."] });

    public static readonly Error UnsupportedType = Error.Validation("unsupported-type", "Upload a PDF or Word file");

    public static readonly Error TooLarge = Error.Validation("too-large", "That's over 5 MB");

    // ---- Hire Fest ----
    public static readonly Error FestNotFound = Error.NotFound("fest-not-found", "Hire Fest not found");

    public static readonly Error BookingNotFound = Error.NotFound("booking-not-found", "Booking not found");

    public static readonly Error BookingClosed = Error.Gone("booking-closed", "Booking for this fest has closed");

    public static readonly Error ProfileIncomplete = Error.Validation("profile-incomplete", "Fill your Job Profile to 60% to book");

    public static readonly Error LearnerUnavailable = Error.Unavailable("learner-unavailable", "We couldn't check your Job Profile. Try again in a moment.");

    public static readonly Error EmployerNotFound = Error.Validation("employer-not-found", "That employer isn't at this fest");

    public static readonly Error TooManyEmployers = Error.Validation("too-many-employers", "Pick up to 2 employers per fest");

    public static readonly Error SlotNotFound = Error.Validation("slot-not-found", "That interview time isn't available");

    public static readonly Error SlotOverlap = Error.Validation("slot-overlap", "Keep 30 minutes between your two interviews");

    public static readonly Error SlotFull = Error.Conflict("slot-full", "That time just filled up. Pick another or join the waitlist.");

    public static readonly Error FestFull = Error.Conflict("fest-full", "This fest is full. Join the waitlist.");

    public static readonly Error TooLateToCancel = Error.Conflict("too-late-to-cancel", "Interviews can be cancelled up to 24 hours before");

    public static readonly Error PastFestNotFound = Error.NotFound("past-fest-not-found", "You didn't attend this fest");

    public static readonly Error InterviewNotFound = Error.Validation("interview-not-found", "You had no interview with that employer");

    // ---- My Matches ----
    public static readonly Error MatchNotFound = Error.NotFound("match-not-found", "Match not found");
}
