namespace Learner.Application.Contracts;

// Response shapes match the frontend types (FRONTEND_TYPES.md), serialised camelCase.

/// <summary>progress.api.ts LearnerProgress.</summary>
public sealed record LearnerProgressDto(int Completed, int PartsDone, int Xp, int Badges, int Level, string Rank, int Target);

/// <summary>quest.api.ts QuestCompletion.</summary>
public sealed record QuestCompletionDto(int XpEarned, LearnerProgressDto Progress);

/// <summary>identity.api.ts LinkedId.</summary>
public sealed record LinkedIdDto(string Doc, string Masked, string Last4, string? Birthday, string? Name, DateTime VerifiedAt);

public sealed record AuthorizeUrlDto(string AuthorizeUrl);

/// <summary>programme.api.ts Programme.</summary>
public sealed record ProgrammeDto(
    string Code,
    string Name,
    string Partner,
    string Centre,
    string BatchStarts,
    string Duration,
    string Stipend,
    string MarksFrom,
    int MinEnglish,
    int MinMaths,
    string Eligibility);

public sealed record BankBranchDto(string Bank, string Branch);

public sealed record ProgrammeResultDto(int XpEarned, LearnerProgressDto Progress, string AccountTail);

public sealed record FeedbackResultDto(int Xp);

public sealed record StrengthTaskDto(string Id, string Label, int Xp, bool Done);

public sealed record StrengthDto(int Percent, IReadOnlyList<StrengthTaskDto> Tasks);

public sealed record ShowcaseBadgeDto(string Id, string Name, string Icon, string Rarity);

public sealed record NextBadgeDto(string Name, string Icon, int Percent);

public sealed record ShowcaseDto(IReadOnlyList<ShowcaseBadgeDto> Pinned, NextBadgeDto? Next);

public sealed record MaskedFieldDto(string Key, string Label, string Masked, bool Empty);

public sealed record ArchetypeDto(string Name, string Blurb);

public sealed record ProfileStatsDto(int StreakDays, int Badges, double LearningHours);

/// <summary>profile.api.ts PlayerProfile.</summary>
public sealed record PlayerProfileDto(
    string PlayerId,
    string Name,
    string Initials,
    string Title,
    string Joined,
    string? PhotoUrl,
    int Level,
    int Xp,
    int NextLevelXp,
    int LevelStartXp,
    int? BatchRank,
    string? BestSkill,
    ArchetypeDto? Archetype,
    ProfileStatsDto Stats,
    StrengthDto Strength,
    ShowcaseDto Showcase,
    IReadOnlyList<MaskedFieldDto> PrivateFields,
    IReadOnlyList<string> Skills);

public sealed record ProfileSavedDto(PlayerProfileDto Profile, int XpEarned);

/// <summary>profile.api.ts editable(): unmasked, fetched only when Edit opens.</summary>
public sealed record ProfileEditDto(
    IReadOnlyList<string> Skills,
    string Gender,
    string Dob,
    IReadOnlyList<string> SkillOptions,
    string Mobile,
    string Email);

public sealed record RevealDto(string Value);

public sealed record LinkCheckDto(bool Verified);

public sealed record FileIdDto(string FileId);

public sealed record ImportedTrophyDto(string Name, string Issuer, string IssuedOn, string CredentialId);

public sealed record EscoSkillDto(string Uri, string Label, string Type, string Category, string? EscoCategory, IReadOnlyList<string>? Alt);

public sealed record EscoSearchDto(int Total, IReadOnlyList<EscoSkillDto> Results);

public sealed record PreferencesDto(bool Sounds, bool Voice, string Language);

public sealed record QuestStepDto(string Key, string Label, int Part);

public sealed record QuestFillDto(int Id, string Slug, string Name, int Filled, int Total, IReadOnlyList<QuestStepDto> Left);

/// <summary>A-262 completeness, quest by quest. Internal API for CV, Careers and Launchpad.</summary>
public sealed record ProfileProgressDto(int Percent, int Filled, int Total, IReadOnlyList<QuestFillDto> Quests);
