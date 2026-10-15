using System.Text.Json.Serialization;

namespace Cv.Application;

// Shapes match cv.api.ts (FRONTEND_TYPES.md), serialised camelCase. Optional TypeScript fields (`quip?`, `check?`…)
// are left out when empty rather than sent as null.

/// <summary>A file from a multipart upload. The caller owns and disposes the stream.</summary>
public sealed record UploadedFile(string FileName, string ContentType, long Length, Stream Content);

public sealed record CvUploadedDto(string CvId);

public sealed record ParseStatusDto(
    string State,
    int Percent,
    int Step,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Quip,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Outcome,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail);

public sealed record CvFieldDto(
    string Key,
    string Label,
    string Value,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Check = null);

public sealed record CvSectionDto(
    int Quest,
    string Title,
    string Badge,
    IReadOnlyList<CvFieldDto> Fields,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Chips = null);

public sealed record CvSummaryDto(
    int QuestsPrefilled,
    int DetailsFound,
    int ToCheck,
    int SkillsMatched,
    IReadOnlyList<CvSectionDto> Sections,
    IReadOnlyList<string> Skipped);
