using SkillMuni.Common.Results;

namespace Gamification.Application;

public static class GamificationErrors
{
    public static readonly Error PlayerNotFound = Error.NotFound("player-not-found", "Player not found");

    public static readonly Error NotYourself = Error.Validation("not-yourself", "You can't do that to yourself");

    public static readonly Error ChallengeOpen = Error.Conflict("challenge-open", "You already have a race going with this player");

    public static readonly Error InvalidScope = Error.Validation("invalid-scope", "Scope is everyone, batch or friends");

    public static readonly Error InvalidPeriod = Error.Validation("invalid-period", "Period is week, month or all");

    public static readonly Error TooManyPinned = Error.Validation("too-many-pinned", "Pin up to 3 badges");

    public static readonly Error BadgeNotEarned = Error.Validation("badge-not-earned", "Only earned badges can be pinned");
}
