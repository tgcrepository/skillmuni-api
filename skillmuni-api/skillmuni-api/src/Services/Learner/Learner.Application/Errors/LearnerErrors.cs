using SkillMuni.Common.Results;

namespace Learner.Application.Errors;

/// <summary>Every expected failure, with the business code the frontend maps to a screen state.</summary>
public static class LearnerErrors
{
    public static readonly Error LearnerErased = Error.Gone("learner-erased", "This account has been deleted");

    public static readonly Error QuestNotFound = Error.NotFound("quest-not-found", "Quest not found");

    public static readonly Error PartNotFound = Error.NotFound("part-not-found", "Quest step not found");

    public static readonly Error QuestLocked = Error.Validation("quest-locked", "Finish the quests before this one first");

    public static readonly Error AnswersTooLarge = Error.Validation("answers-too-large", "That's too much to save in one quest");

    public static readonly Error DraftNotFound = Error.NotFound("draft-not-found", "Draft not found");

    public static readonly Error DraftTooLarge = Error.Validation("draft-too-large", "That draft is too large to save");

    public static readonly Error ProgrammeNotFound = Error.NotFound("programme-not-found", "We couldn't find that code");

    public static readonly Error TooManyTries = Error.RateLimited("too-many-tries", "Too many wrong codes. Try again in 30 minutes.");

    public static readonly Error AlreadyEnrolled = Error.Conflict("already-enrolled", "You've already joined this programme");

    public static readonly Error MarksTooLow = Error.Validation(
        "marks-too-low",
        "Your marks don't meet this programme's rule",
        new Dictionary<string, string[]> { ["marks"] = ["Your marks don't meet this programme's rule."] });

    public static readonly Error BankRequired = Error.Validation(
        "bank-required",
        "Add the bank account the stipend goes to",
        new Dictionary<string, string[]> { ["bank"] = ["Add the bank account the stipend goes to."] });

    public static readonly Error InvalidIfsc = Error.Validation(
        "invalid-ifsc",
        "That doesn't look like an IFSC",
        new Dictionary<string, string[]> { ["routing"] = ["That doesn't look like an IFSC."] });

    public static readonly Error IfscNotFound = Error.NotFound("ifsc-not-found", "We couldn't find that IFSC");

    public static readonly Error BankLookupUnavailable = Error.Unavailable("bank-lookup-unavailable", "Bank lookup is down. Type the bank name instead.");

    public static readonly Error DigiLockerUnavailable = Error.Unavailable("digilocker-unavailable", "DigiLocker isn't available right now");

    public static readonly Error DigiLockerRejected = Error.Validation("digilocker-rejected", "DigiLocker didn't confirm that document");

    public static readonly Error IdentityNotLinked = Error.NotFound("identity-not-linked", "No ID linked");

    public static readonly Error EscoUnavailable = Error.Unavailable("esco-unavailable", "Skill search is down. Pick from the decks instead.");

    public static readonly Error LinkTimeout = Error.Timeout("link-timeout", "That page took too long to answer");

    public static readonly Error LinkUnreachable = Error.Validation("link-unreachable", "We couldn't open that link");

    public static readonly Error FileMissing = Error.Validation(
        "file-missing",
        "Choose a file",
        new Dictionary<string, string[]> { ["file"] = ["Choose a file."] });

    public static readonly Error FileTooLarge = Error.Validation("too-large", "That file is too large");

    public static readonly Error UnsupportedType = Error.Validation("unsupported-type", "That file type isn't supported");

    public static readonly Error PhotoNotFound = Error.NotFound("photo-not-found", "No profile photo");

    public static readonly Error DobInFuture = Error.Validation(
        "dob-in-future",
        "Date of birth can't be in the future",
        new Dictionary<string, string[]> { ["dob"] = ["Date of birth can't be in the future."] });

    public static readonly Error MarketNotSupported = Error.Validation("market-not-supported", "Not available in your country");
}
