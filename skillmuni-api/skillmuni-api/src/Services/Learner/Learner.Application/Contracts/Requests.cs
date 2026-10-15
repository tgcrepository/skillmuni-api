using System.Text.Json;

namespace Learner.Application.Contracts;

/// <summary>Who is calling, from the access token. Used to create the learner lazily if the sign-up event is late.</summary>
public sealed record LearnerRef(Guid Id, string FirstName, string Market);

/// <summary>API-127 body. <c>values</c> is shallow-merged into the quest's answers.</summary>
public sealed record SavePartRequest(JsonElement Values, bool Done, bool? Xp, int? BonusXp);

public sealed record StartDigiLockerRequest(string Doc);

public sealed record LinkDigiLockerRequest(string Doc, string Code);

public sealed record BankDetails(string Holder, string Type, string Account, string Routing, string? Upi);

/// <summary>API-124. Marks arrive as strings from the form.</summary>
public sealed record ProgrammeSubmission(
    string Code,
    string MarksFrom,
    string English,
    string Maths,
    IReadOnlyDictionary<string, string>? Proficiency,
    BankDetails? Bank);

/// <summary>Tenant admin: create or update a programme code.</summary>
public sealed record SaveProgrammeRequest(
    string Code,
    string Name,
    string Partner,
    string Centre,
    DateOnly BatchStarts,
    string Duration,
    string? Stipend,
    string MarksFrom,
    int MinEnglish,
    int MinMaths,
    string Eligibility);

public sealed record FeedbackRequest(int Mood, IReadOnlyList<string>? Tags, string? Comment, string? Screen);

/// <summary>API-120. <c>dob</c> is an ISO date or empty.</summary>
public sealed record ProfileEditRequest(IReadOnlyList<string> Skills, string Gender, string? Dob);

public sealed record RevealRequest(string Field);

public sealed record CheckLinkRequest(string Url);

/// <summary>MISS-09.</summary>
public sealed record PreferencesRequest(bool Sounds, bool Voice, string Language);

/// <summary>A file from a multipart upload. The caller owns and disposes the stream.</summary>
public sealed record UploadedFile(string FileName, string ContentType, long Length, Stream Content);
