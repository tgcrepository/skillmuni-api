namespace Gamification.Domain;

/// <summary>
/// One XP credit (MISS-01). <c>(LearnerId, Reason, SourceId)</c> is unique, so the same action can't pay twice
/// (CLAUDE.md §9). Reasons are stable codes, e.g. <c>quest.step</c> with source <c>who-dis:2</c>.
/// </summary>
public sealed class XpEntry : Entity
{
    private XpEntry()
    {
    }

    public Guid LearnerId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string SourceId { get; private set; } = string.Empty;

    public int Xp { get; private set; }

    public DateTime OccurredAt { get; private set; }

    public static XpEntry Credit(string tenantId, Guid learnerId, string reason, string sourceId, int xp, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        Reason = reason,
        SourceId = sourceId,
        Xp = xp,
        OccurredAt = now,
    };
}

public sealed class BadgeAward : Entity
{
    private BadgeAward()
    {
    }

    public Guid LearnerId { get; private set; }

    public string BadgeId { get; private set; } = string.Empty;

    public DateTime EarnedAt { get; private set; }

    public static BadgeAward Earn(string tenantId, Guid learnerId, string badgeId, DateTime now) => new()
    {
        TenantId = tenantId,
        LearnerId = learnerId,
        BadgeId = badgeId,
        EarnedAt = now,
    };
}

/// <summary>A day (in the learner's market time zone) on which they learned or earned XP: the streak calendar.</summary>
public sealed class ActiveDay : Entity
{
    private ActiveDay()
    {
    }

    public Guid LearnerId { get; private set; }

    public DateOnly Day { get; private set; }

    public static ActiveDay On(string tenantId, Guid learnerId, DateOnly day) => new() { TenantId = tenantId, LearnerId = learnerId, Day = day };
}

/// <summary>Progress on one of today's daily quests (MISS-03). Credited automatically when the target is reached.</summary>
public sealed class DailyProgress : Entity
{
    private DailyProgress()
    {
    }

    public Guid LearnerId { get; private set; }

    public DateOnly Day { get; private set; }

    public string QuestId { get; private set; } = string.Empty;

    public int Value { get; private set; }

    public bool Completed { get; private set; }

    public static DailyProgress Start(string tenantId, Guid learnerId, DateOnly day, string questId) =>
        new() { TenantId = tenantId, LearnerId = learnerId, Day = day, QuestId = questId };

    /// <returns>True when this addition completes the quest.</returns>
    public bool Add(int amount, int target)
    {
        if (Completed)
        {
            return false;
        }

        Value += amount;
        Completed = Value >= target;
        return Completed;
    }
}

/// <summary>
/// One learner cheering another in the Arena. Toggling keeps the row, so cheering the same player again doesn't count
/// twice towards the Cheerleader badge.
/// </summary>
public sealed class Cheer : Entity
{
    private Cheer()
    {
    }

    public Guid FromLearnerId { get; private set; }

    public Guid ToLearnerId { get; private set; }

    public bool Active { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static Cheer Give(string tenantId, Guid from, Guid to, DateTime now) =>
        new() { TenantId = tenantId, FromLearnerId = from, ToLearnerId = to, Active = true, CreatedAt = now, UpdatedAt = now };

    /// <returns>True when the state changed.</returns>
    public bool Set(bool on, DateTime now)
    {
        if (Active == on)
        {
            return false;
        }

        Active = on;
        UpdatedAt = now;
        return true;
    }
}

/// <summary>An invitation to a friendly one-week XP race. Results wait on the Arena rules (MISS-11).</summary>
public sealed class Challenge : Entity
{
    private Challenge()
    {
    }

    public Guid FromLearnerId { get; private set; }

    public Guid ToLearnerId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime EndsAt { get; private set; }

    public static Challenge Invite(string tenantId, Guid from, Guid to, DateTime now, TimeSpan length) =>
        new() { TenantId = tenantId, FromLearnerId = from, ToLearnerId = to, CreatedAt = now, EndsAt = now + length };
}
