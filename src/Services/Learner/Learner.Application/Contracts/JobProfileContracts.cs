using System.Text.Json.Serialization;

namespace Learner.Application.Contracts;

// job-profile.api.ts (FRONTEND_TYPES.md), serialised camelCase. API-068…074.

public sealed record QuestRefDto(string Slug, int Part, string Name);

public sealed record RadarStatsDto(int People, int Business, int Data, int Creative, int Tech);

public sealed record WorkItemDto(string Id, string Initials, string Role, string Employer, string City, string Period, IReadOnlyList<string> Bullets, string Proof);

public sealed record EducationItemDto(string Id, string Initials, string Title, string School, string Detail, string Proof);

public sealed record LanguageItemDto(string Name, string Skills, string Level);

public sealed record SkillItemDto(string Name, int? Power, bool Signature);

public sealed record CertificateItemDto(string Id, string Title, string Issuer, string Status, string? Eta);

public sealed record QuickWinDto(string Id, string Title, string Impact, string Link, IReadOnlyDictionary<string, int> Query);

public sealed record PendingQuestDto(string Slug, string Title, string Detail, int Xp);

public sealed record JobArchetypeDto(string Name, string Strongest);

public sealed record SignatureSkillDto(string Name, int Power);

public sealed record JobProfileCountsDto(int Skills, int Languages, int Trophies);

public sealed record JobProfileStrengthDto(int Percent, string Label, int JobMatches, IReadOnlyList<QuickWinDto> QuickWins);

public sealed record JobProfileContactDto(
    string Mobile,
    bool MobileVerified,
    string Email,
    string Location,
    string City,
    bool Discoverable,
    string InvitesBy,
    string NeverShown);

public sealed record JobProfileAboutDto(string Text, bool ByMuni, IReadOnlyList<string> Tags);

public sealed record LookingForRowDto(
    string Label,
    string Value,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Good = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Private = null);

/// <summary>job-profile.api.ts JobProfile: one record built from the 8 quests (A-262).</summary>
public sealed record JobProfileDto(
    string Name,
    string Initials,
    string Headline,
    JobArchetypeDto? Archetype,
    RadarStatsDto? Radar,
    IReadOnlyList<SignatureSkillDto> Signature,
    JobProfileCountsDto Counts,
    IReadOnlyList<QuestFillDto> ByQuest,
    JobProfileStrengthDto Strength,
    JobProfileContactDto Contact,
    JobProfileAboutDto About,
    IReadOnlyList<WorkItemDto> Work,
    IReadOnlyList<EducationItemDto> Education,
    IReadOnlyList<LanguageItemDto> Languages,
    IReadOnlyList<SkillItemDto> Skills,
    IReadOnlyList<CertificateItemDto> Certificates,
    IReadOnlyList<LookingForRowDto> LookingFor,
    string VerifyCode,
    int VerifiedCount,
    DateTime UpdatedAt,
    IReadOnlyList<PendingQuestDto> Pending,
    IReadOnlyDictionary<string, QuestRefDto> Sources);

/// <summary>P5: one difference between the newer CV and the record.</summary>
public sealed record CvChangeDto(string Id, string Kind, string Title, string Detail, string Suggest);

public sealed record ShareSettingsDto(string Url, bool HideContact, int ExpiresDays, int ViewsThisWeek);

/// <summary>Full contact for the learner's own CV file; never part of the profile payload or the employer view.</summary>
public sealed record CvContactDto(string Phone, string Email, string Location, string? Linkedin, string ExpectedPay);

public sealed record SaveAboutRequest(string? Text);

public sealed record ApplyCvChangesRequest(IReadOnlyList<string>? Ids);

public sealed record UpdateShareRequest(bool HideContact, int ExpiresDays);
