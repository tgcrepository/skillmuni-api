using SkillMuni.Common.Results;

namespace Platform.Application;

public static class PlatformErrors
{
    public static readonly Error PostalNotFound = Error.NotFound("postal-not-found", "We couldn't find that code. Pick your state yourself.");

    public static readonly Error UnknownMarket = Error.NotFound("unknown-market", "That market isn't available.");

    public static readonly Error NotificationNotFound = Error.NotFound("notification-not-found", "Notification not found");
}
