namespace Learner.Domain;

public enum Gender
{
    RatherNot = 0,
    Female = 1,
    Male = 2,
    NonBinary = 3,
}

/// <summary>
/// The learner: the profile the 8 quests build, plus read-only copies of what other services own (verified
/// contacts from Identity; XP, level and badges from Gamification; archetype from Assessment), kept so the
/// Profile and progress screens load in one query. The id is the Identity account id.
/// </summary>
public sealed class LearnerProfile : Entity
{
    public const int MaxProfileSkills = 5;

    private LearnerProfile()
    {
    }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string Market { get; private set; } = "IN";

    /// <summary>Copy of the Identity-verified mobile (E.164).</summary>
    public string? Phone { get; private set; }

    /// <summary>Copy of the Identity-verified email.</summary>
    public string? Email { get; private set; }

    public string? PhotoBlob { get; private set; }

    public int PhotoVersion { get; private set; }

    public Gender? Gender { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    /// <summary>Up to five headline skills for the Player ID card, as JSON.</summary>
    public string ProfileSkillsJson { get; private set; } = "[]";

    /// <summary>Highest quest finished, 0–8.</summary>
    public int QuestsCompleted { get; private set; }

    /// <summary>Parts done in the quest after <see cref="QuestsCompleted"/>.</summary>
    public int PartsDone { get; private set; }

    public int ProgrammeWrongTries { get; private set; }

    public DateTime? ProgrammeLockedUntil { get; private set; }

    public bool SoundsOn { get; private set; } = true;

    public bool VoiceOn { get; private set; } = true;

    public string Language { get; private set; } = "en";

    // Projections from Gamification and Assessment events.
    public int Xp { get; private set; }

    public int Level { get; private set; } = 1;

    public string Rank { get; private set; } = "Rookie";

    public int LevelStartXp { get; private set; }

    public int NextLevelXp { get; private set; } = 400;

    public int Badges { get; private set; }

    public int StreakDays { get; private set; }

    /// <summary>
    /// XP shown straight after an award, before Gamification's totals arrive: the projection plus what this service
    /// has just awarded. It stops counting after a short window, so a refused award can't inflate the display.
    /// </summary>
    public int EstimatedXp { get; private set; }

    public DateTime? EstimatedAt { get; private set; }

    public string? ArchetypeName { get; private set; }

    public string? ArchetypeSummary { get; private set; }

    /// <summary>When the Job Profile first reached 100% (BR-110; the event is published once).</summary>
    public DateTime? JobProfileCompletedAt { get; private set; }

    public DateTime JoinedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public bool HasPhoto => PhotoBlob is not null;

    public bool HasCareerDrivers => ArchetypeName is not null;

    public bool HasProfileSkills => ProfileSkillsJson != "[]";

    public static LearnerProfile Create(Guid accountId, string tenantId, string market, string firstName, string lastName, DateTime now) => new()
    {
        Id = accountId,
        TenantId = tenantId,
        Market = market,
        FirstName = firstName.Trim(),
        LastName = lastName.Trim(),
        JoinedAt = now,
        UpdatedAt = now,
    };

    public void Rename(string firstName, string lastName, DateTime now)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        UpdatedAt = now;
    }

    public void SetVerifiedContact(string? phone, string? email, DateTime now)
    {
        Phone = phone ?? Phone;
        Email = email ?? Email;
        UpdatedAt = now;
    }

    /// <summary>Saving a part moves "parts done" only forward, and only in the quest in progress.</summary>
    /// <returns>True the first time this part of the current quest is done (it earns step XP).</returns>
    public bool RecordPartDone(int questNumber, int part, DateTime now)
    {
        if (questNumber != QuestsCompleted + 1 || part <= PartsDone)
        {
            return false;
        }

        PartsDone = part;
        UpdatedAt = now;
        return true;
    }

    /// <returns>True the first time this quest is completed (it earns quest XP and its badge).</returns>
    public bool CompleteQuest(int questNumber, DateTime now)
    {
        if (questNumber <= QuestsCompleted)
        {
            return false;
        }

        QuestsCompleted = questNumber;
        PartsDone = 0;
        UpdatedAt = now;
        return true;
    }

    /// <summary>A confirmed institute claim pre-fills quests 1–5 (BR-119).</summary>
    /// <returns>The quests newly counted as done, in order (empty when they already were).</returns>
    public IReadOnlyList<int> FastForwardQuests(int questNumber, DateTime now)
    {
        if (questNumber <= QuestsCompleted)
        {
            return [];
        }

        var done = Enumerable.Range(QuestsCompleted + 1, questNumber - QuestsCompleted).ToList();
        QuestsCompleted = questNumber;
        PartsDone = 0;
        UpdatedAt = now;
        return done;
    }

    /// <returns>True the first time the Job Profile reaches 100%.</returns>
    public bool MarkJobProfileComplete(DateTime now)
    {
        if (JobProfileCompletedAt is not null)
        {
            return false;
        }

        JobProfileCompletedAt = now;
        UpdatedAt = now;
        return true;
    }

    /// <returns>True when this is the first photo (a strength task).</returns>
    public bool SetPhoto(string blobName, DateTime now)
    {
        var first = PhotoBlob is null;
        PhotoBlob = blobName;
        PhotoVersion++;
        UpdatedAt = now;
        return first;
    }

    /// <returns>True when skills were empty before (a strength task).</returns>
    public bool EditDetails(string skillsJson, bool hasSkills, Gender gender, DateOnly? dateOfBirth, DateTime now)
    {
        var first = ProfileSkillsJson == "[]" && hasSkills;
        ProfileSkillsJson = skillsJson;
        Gender = gender;
        DateOfBirth = dateOfBirth;
        UpdatedAt = now;
        return first;
    }

    /// <summary>Quest 1 ("ID &amp; birthday", "The tea") fills the Profile's gender and birthday when they're still empty.</summary>
    public void SeedFromQuest(Gender? gender, DateOnly? birthday, DateTime now)
    {
        if ((gender is null || Gender is not null) && (birthday is null || DateOfBirth is not null))
        {
            return;
        }

        Gender ??= gender;
        DateOfBirth ??= birthday;
        UpdatedAt = now;
    }

    public void SetVerifiedBirthday(DateOnly birthday, DateTime now)
    {
        DateOfBirth ??= birthday;
        UpdatedAt = now;
    }

    public bool IsProgrammeLocked(DateTime now) => ProgrammeLockedUntil is { } until && until > now;

    /// <summary>Five wrong programme codes lock the check for <paramref name="coolDown"/> (Figma P4).</summary>
    public void RecordWrongProgrammeCode(DateTime now, int maxTries, TimeSpan coolDown)
    {
        ProgrammeWrongTries++;
        if (ProgrammeWrongTries >= maxTries)
        {
            ProgrammeLockedUntil = now + coolDown;
            ProgrammeWrongTries = 0;
        }

        UpdatedAt = now;
    }

    public void ResetProgrammeTries() => ProgrammeWrongTries = 0;

    public void SetPreferences(bool soundsOn, bool voiceOn, string language, DateTime now)
    {
        SoundsOn = soundsOn;
        VoiceOn = voiceOn;
        Language = language;
        UpdatedAt = now;
    }

    /// <summary>Gamification sends absolute totals, so replaying an older event can't double-count.</summary>
    public void ApplyProgress(int xp, int level, string rank, int levelStartXp, int nextLevelXp, int badges, int streakDays, DateTime now)
    {
        Xp = Math.Max(Xp, xp);
        Level = level;
        Rank = rank;
        LevelStartXp = levelStartXp;
        NextLevelXp = nextLevelXp;
        Badges = Math.Max(Badges, badges);
        StreakDays = streakDays;
        UpdatedAt = now;
    }

    /// <summary>Records XP this service just told Gamification to award, for <see cref="DisplayXp"/>.</summary>
    public void RecordAwardedXp(int xp, DateTime now)
    {
        if (xp <= 0)
        {
            return;
        }

        EstimatedXp = Math.Max(EstimatedXp, Xp) + xp;
        EstimatedAt = now;
        UpdatedAt = now;
    }

    public int DisplayXp(DateTime now, TimeSpan window) =>
        EstimatedAt is { } at && now - at <= window ? Math.Max(Xp, EstimatedXp) : Xp;

    public void ApplyArchetype(string name, string summary, DateTime now)
    {
        ArchetypeName = name;
        ArchetypeSummary = summary;
        UpdatedAt = now;
    }
}
