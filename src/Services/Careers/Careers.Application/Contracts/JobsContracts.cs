namespace Careers.Application.Contracts;

// Shapes match jobs.api.ts (FRONTEND_TYPES.md), serialised camelCase.

public sealed record MatchReasonDto(bool Ok, string Text);

public sealed record MatchGapDto(string Label, string Link);

public sealed record MatchInfoDto(int Percent, IReadOnlyList<string> Skills, IReadOnlyList<MatchReasonDto> Reasons, MatchGapDto? Gap);

public sealed record JobDto(
    string Id,
    string Title,
    string Company,
    string Initials,
    string City,
    string State,
    string Place,
    string Sector,
    string Role,
    string WorkMode,
    string JobType,
    string JobTypeLabel,
    string Pay,
    int PostedDays,
    int Applicants,
    string ApplyBy,
    int? ClosingInDays,
    bool Closed,
    bool IsNew,
    bool Saved,
    int? SavedDays,
    string? AppliedOn,
    string Experience,
    int Openings,
    string Shift,
    string About,
    IReadOnlyList<string> Duties,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<string> Skills,
    string CompanyAbout,
    MatchInfoDto? Match);

public sealed record RecruiterDto(string Name, string Initials, string Title);

public sealed record InvitationDto(
    string Id,
    JobDto Job,
    RecruiterDto Recruiter,
    string Type,
    DateTimeOffset? InterviewAt,
    string Message,
    string RespondBy,
    int DaysLeft,
    string Status);

public sealed record TimelineStepDto(string Label, string? Date, string State);

public sealed record InterviewDto(string At, string Mode, string Detail);

public sealed record AnswerDto(string Q, string A);

public sealed record ApplicationDto(
    string Id,
    JobDto Job,
    string AppliedOn,
    string Updated,
    string Cv,
    string Status,
    IReadOnlyList<TimelineStepDto> Timeline,
    InterviewDto? Interview,
    IReadOnlyList<AnswerDto> Answers,
    int Similar);

public sealed record JobsSummaryDto(
    int Available,
    int Matching,
    int Invitations,
    int InvitationsNew,
    int Bookmarked,
    int Applied,
    int ProfilePercent,
    int UnlockAt,
    IReadOnlyList<string> Missing,
    int MinutesToUnlock,
    string DefaultPlace);

public sealed record FacetOptionDto(string Id, string Label, int Count);

public sealed record FacetDto(string Key, IReadOnlyList<FacetOptionDto> Options);

public sealed record SuggestionDto(string Key, string Id, string Label, int Count);

/// <summary>jobs.api.ts <c>ListResult&lt;T&gt;</c>: the first <c>limit</c> hits, the total, facet counts, J6 suggestions and a note.</summary>
public sealed record ListResultDto<T>(IReadOnlyList<T> Items, int Total, IReadOnlyList<FacetDto> Facets, IReadOnlyList<SuggestionDto> Suggestions, string? Note);

/// <summary>jobs.api.ts <c>ListQuery</c> as query string: <c>q, place, sort, limit, filters</c> (JSON of key → ids).</summary>
public sealed record ListQuery(string Q, string Place, string Sort, int Limit, IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> Filters)
{
    public IReadOnlyList<string> Filter(string key) => Filters.FirstOrDefault(f => f.Key == key).Value ?? [];
}

public sealed record CvOptionDto(string Id, string Name, string Meta);

public sealed record ApplyQuestionDto(string Id, string Text, string Kind);

public sealed record ApplyInfoDto(string Name, string Contact, IReadOnlyList<CvOptionDto> Cvs, IReadOnlyList<ApplyQuestionDto> Questions);

public sealed record ApplyRequest(string CvId, IReadOnlyDictionary<string, string>? Answers, string? InvitationId);

public sealed record SaveRequest(bool Saved);

public sealed record RespondRequest(string? Note);

public sealed record AlertSettingsDto(bool On, string Frequency, IReadOnlyList<string> Searches);

/// <summary>A file from a multipart upload, without ASP.NET types.</summary>
public sealed record UploadedFile(string FileName, long Length, Stream Content);
