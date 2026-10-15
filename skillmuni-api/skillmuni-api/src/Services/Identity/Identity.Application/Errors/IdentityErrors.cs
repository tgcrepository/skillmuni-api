using SkillMuni.Common.Results;

namespace Identity.Application.Errors;

/// <summary>Every expected failure, with the business code the frontend maps to a screen state.</summary>
public static class IdentityErrors
{
    public static readonly Error AccountNotFound = Error.NotFound("account-not-found", "No account for that login");

    public static readonly Error AccountLocked = Error.Locked("account-locked", "Account locked");

    public static readonly Error CodeExpired = Error.Gone("code-expired", "Code expired");

    public static readonly Error TooManyTries = Error.RateLimited("too-many-tries", "Too many tries");

    public static readonly Error TooManyCodes = Error.RateLimited("too-many-codes", "Too many codes sent. Try again in an hour.");

    public static readonly Error WrongCredentials = Error.Unauthorized("wrong-credentials", "Wrong email or password");

    public static readonly Error EmailTaken = Error.Conflict(
        "email-taken",
        "Email taken",
        new Dictionary<string, string[]> { ["email"] = ["Already in the squad."] });

    public static readonly Error PhoneTaken = Error.Conflict(
        "phone-taken",
        "That number is linked to another account",
        new Dictionary<string, string[]> { ["phone"] = ["That number is linked to another account."] });

    public static readonly Error InvalidPhone = Error.Validation(
        "invalid-phone",
        "That doesn't look like a mobile number",
        new Dictionary<string, string[]> { ["phone"] = ["That doesn't look like a mobile number."] });

    public static readonly Error InvalidEmail = Error.Validation(
        "invalid-email",
        "That doesn't look like an email address",
        new Dictionary<string, string[]> { ["email"] = ["That doesn't look like an email address."] });

    public static readonly Error ConsentRequired = Error.Validation(
        "consent-required",
        "Please accept the privacy terms to continue",
        new Dictionary<string, string[]> { ["consent"] = ["Please accept the privacy terms to continue."] });

    public static readonly Error RegistrationNotFound = Error.NotFound("registration-not-found", "Start sign-up again");

    public static readonly Error GoogleInvalid = Error.Unauthorized("google-invalid", "Google token rejected");

    public static readonly Error GoogleUnavailable = Error.Unavailable("google-unavailable", "Google sign-in isn't set up for this app");

    public static readonly Error NoSession = Error.Unauthorized("no-session", "No session");

    public static readonly Error SessionExpired = Error.Unauthorized("session-expired", "Session expired");

    public static readonly Error SessionNotFound = Error.NotFound("session-not-found", "Session not found");

    public static readonly Error ClaimNotFound = Error.NotFound("claim-not-found", "Link not found");

    public static readonly Error NotClaimant = Error.Forbidden("not-claimant", "This link belongs to someone else");

    public static readonly Error ConsentFirst = Error.Validation("consent-first", "Choose what to share first");

    public static readonly Error SectionNotFound = Error.NotFound("section-not-found", "Section not found");

    public static readonly Error SectionsPending = Error.Validation("sections-pending", "Check every section first");

    public static Error InvalidCode(int triesLeft) => Error.Validation(
        "invalid-code",
        "Wrong code",
        new Dictionary<string, string[]> { ["triesLeft"] = [triesLeft.ToString(System.Globalization.CultureInfo.InvariantCulture)] });

    /// <summary>410 for a claim link: claimed · expired · deleted.</summary>
    public static Error ClaimGone(string code) => Error.Gone(code, code switch
    {
        "claimed" => "This profile has already been claimed",
        "expired" => "This link has expired",
        _ => "This profile was deleted",
    });
}
