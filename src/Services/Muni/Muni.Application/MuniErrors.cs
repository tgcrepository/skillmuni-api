using SkillMuni.Common.Results;

namespace Muni.Application;

public static class MuniErrors
{
    public static readonly Error RevealUnavailable = Error.Unavailable("reveal-unavailable", "We couldn't load that right now. Try again in a moment.");

    public static readonly Error InvalidMessageId = Error.Validation("invalid-message-id", "That message id isn't valid.");
}
