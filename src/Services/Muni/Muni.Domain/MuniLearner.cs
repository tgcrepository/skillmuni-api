using System.Numerics;

namespace Muni.Domain;

/// <summary>
/// What Muni knows about a learner, copied from other services' events so a reply never waits on them:
/// name and joining date (Identity), XP totals and streak (Gamification), finished quests (Learner) and the
/// Career Drivers archetype (Assessment). <see cref="Entity.Id"/> is the learner id.
/// </summary>
public sealed class MuniLearner : Entity
{
    public const int QuestCount = 8;

    private MuniLearner()
    {
    }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public DateTime? JoinedAt { get; private set; }

    public int TotalXp { get; private set; }

    public int Level { get; private set; }

    public string Rank { get; private set; } = string.Empty;

    /// <summary>XP at which the next level starts (equal to <see cref="TotalXp"/> or below at the top level).</summary>
    public int NextLevelXp { get; private set; }

    public int StreakDays { get; private set; }

    /// <summary>Bit n-1 set = quest n finished. Replayed events set the same bit, so counting stays right.</summary>
    public int QuestsDoneMask { get; private set; }

    public string? Archetype { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public int QuestsDone => BitOperations.PopCount((uint)QuestsDoneMask);

    public static MuniLearner Create(string tenantId, Guid learnerId, int level, string rank, int nextLevelXp, DateTime now) => new()
    {
        Id = learnerId,
        TenantId = tenantId,
        Level = level,
        Rank = rank,
        NextLevelXp = nextLevelXp,
        UpdatedAt = now,
    };

    public void Register(string firstName, string lastName, DateTime joinedAt, DateTime now)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        JoinedAt ??= joinedAt;
        UpdatedAt = now;
    }

    /// <summary>Totals, not deltas: an older event arriving late can't lower the XP shown.</summary>
    public void ApplyTotals(int totalXp, int level, string rank, int nextLevelXp, int streakDays, DateTime now)
    {
        if (totalXp < TotalXp)
        {
            return;
        }

        TotalXp = totalXp;
        Level = level;
        Rank = rank;
        NextLevelXp = nextLevelXp;
        StreakDays = Math.Max(0, streakDays);
        UpdatedAt = now;
    }

    public void CompleteQuest(int quest, DateTime now)
    {
        if (quest is < 1 or > QuestCount)
        {
            return;
        }

        QuestsDoneMask |= 1 << (quest - 1);
        UpdatedAt = now;
    }

    public void SetArchetype(string? archetype, DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(archetype))
        {
            Archetype = archetype.Trim();
            UpdatedAt = now;
        }
    }
}
