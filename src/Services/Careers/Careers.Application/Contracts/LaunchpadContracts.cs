using System.Text.Json.Serialization;

namespace Careers.Application.Contracts;

// Shapes match launchpad.api.ts and matches.api.ts (FRONTEND_TYPES.md), serialised camelCase.

public sealed record MissionDto(string Id, string Label, int Xp, bool Done);

public sealed record ReadinessDto(int Level, string LevelName, int Percent, IReadOnlyList<MissionDto> Missions);

public sealed record ProfileCardDto(int Percent, int SectionsLeft, int SectionsTotal, int StepsLeft, IReadOnlyList<string> Missing);

public sealed record ArchetypeDto(string Name, string Emoji, IReadOnlyList<string> Traits);

public sealed record AssessmentCardDto(string Status, int Answered, int Total, int Minutes, ArchetypeDto? Archetype, int MandatoryCourses);

public sealed record TopJobDto(string Title, string Employer, string City, int Match);

public sealed record JobsCardDto(bool Locked, int UnlockAt, int Matched, int Applied, int Saved, int NewMatches, int Interviews, TopJobDto? Top);

public sealed record FestCardFestDto(
    string Id,
    string Name,
    string Date,
    int DaysAway,
    int Recruiters,
    bool Free,
    string Mode,
    int SlotsLeft,
    int SlotsTotal,
    string? BookedSlot);

public sealed record FestCardDto(FestCardFestDto? Fest, bool AlertsOn);

public sealed record SectorDto(string Id, string Name, string Emoji, int Jobs, int NewJobs, int? Fit);

public sealed record FestAlertsToggle(bool On);

// ---- My Matches ----

/// <summary><c>note?</c> is optional in matches.api.ts: left out when there is none.</summary>
public sealed record MatchSkillDto(string Name, string Evidence, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note);

public sealed record MatchJobDto(
    string Id,
    string Title,
    string Employer,
    string Kind,
    string Location,
    int? DistanceKm,
    int Match,
    string Pay,
    string Status,
    string? InterviewAt,
    IReadOnlyList<MatchSkillDto> Skills,
    string Tip,
    string TipKind,
    string Industry);

public sealed record IndustryFitDto(string Id, string Name, int Fit, string Why, string Roles, string Path, bool PathDone, int Jobs);

public sealed record CompassDto(string TakenOn, string Summary);

public sealed record MatchBoardDto(
    int Total,
    int Shortlisted,
    int Invites,
    int ProfileComplete,
    bool Visible,
    IReadOnlyList<MatchJobDto> Jobs,
    IReadOnlyList<IndustryFitDto> Industries,
    CompassDto? Compass);

public sealed record MatchPreviewTopDto(string Id, string Title, string Kind, int Match);

public sealed record MatchPreviewDto(int Count, int RadiusKm, IReadOnlyList<MatchPreviewTopDto> Top);

public sealed record VisibilitySettings(bool Discoverable, bool Invites);
