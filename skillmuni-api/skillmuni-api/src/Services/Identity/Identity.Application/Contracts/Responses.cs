using System.Text.Json.Serialization;

namespace Identity.Application.Contracts;

// Response bodies. Field names and optionality match FRONTEND_TYPES.md; optional TS fields (`email?`) are left
// out when empty, while `T | null` fields are always present.

public sealed record AuthUserDto(
    string Id,
    string FirstName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Email,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Phone,
    string? PhotoUrl);

public sealed record OtpChallengeDto(string SentTo, int ResendIn, int TriesLeft);

public sealed record EmailChallengeDto(string Email, int ResendIn, int ValidFor, int TriesLeft);

public sealed record ResetRequestedDto(int ResendIn, int ValidFor);

public sealed record GoogleSignInDto(AuthUserDto User, bool IsNew);

public sealed record ContactCodeSentDto(string SentTo);

/// <summary>Cookie material; never serialised to the browser (the internal refresh endpoint returns it to the gateway).</summary>
public sealed record SessionTokens(string AccessToken, DateTime AccessExpiresAt, string? RefreshToken, DateTime RefreshExpiresAt);

/// <summary>A response body plus the session to put in cookies.</summary>
public sealed record SignedIn<T>(T Body, SessionTokens Tokens);

public sealed record ConsentDto(string Scope, string PolicyVersion, string Law, bool Granted, string Source, DateTime RecordedAt);

public sealed record SessionInfoDto(Guid Id, DateTime CreatedAt, DateTime LastSeenAt, string? UserAgent, bool Current);

public sealed record DataExportRequestedDto(DateTime RequestedAt);

public sealed record ClaimCreatedDto(Guid Id, string Token);

public sealed record ClaimInviteDto(
    string Institute,
    string Name,
    string Programme,
    string Batch,
    string FromCv,
    string SentTo,
    int ResendIn,
    int TriesLeft,
    string DeleteBy,
    string Certificate,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ClaimResumeDto? Resume);

/// <summary><c>stage</c>: consent · validate · confirmed.</summary>
public sealed record ClaimResumeDto(string Stage, IReadOnlyList<ClaimSectionDto> Sections, ClaimResultDto? Result);

/// <summary><c>status</c>: confirmed · review · fix.</summary>
public sealed record ClaimSectionDto(string Slug, int Quest, string Title, string Summary, string Status, IReadOnlyList<ClaimRowDto> Rows);

/// <summary><c>source</c>: institute · verified · you · null.</summary>
public sealed record ClaimRowDto(
    string Label,
    string Value,
    string? Source,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Issue);

public sealed record ClaimResultDto(
    int ProfilePercent,
    int VerifiedItems,
    int Jobs,
    int XpLeft,
    int MinutesLeft,
    IReadOnlyList<ClaimLeftQuestDto> Left);

public sealed record ClaimLeftQuestDto(int Quest, string Slug, string Name, string Blurb, int Xp);
