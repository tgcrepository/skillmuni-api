namespace Cv.Application;

// Shapes match cv.api.ts (FRONTEND_TYPES.md), serialised camelCase.

/// <summary>A file from a multipart upload. The caller owns and disposes the stream.</summary>
public sealed record UploadedFile(string FileName, string ContentType, long Length, Stream Content);

public sealed record CvUploadedDto(string CvId);

public sealed record ParseStatusDto(string State, int Percent, int Step, string? Quip, string? Outcome, string? Detail);

public sealed record CvFieldDto(string Key, string Label, string Value, string? Check = null);

public sealed record CvSectionDto(int Quest, string Title, string Badge, IReadOnlyList<CvFieldDto> Fields, IReadOnlyList<string>? Chips = null);

public sealed record CvSummaryDto(
    int QuestsPrefilled,
    int DetailsFound,
    int ToCheck,
    int SkillsMatched,
    IReadOnlyList<CvSectionDto> Sections,
    IReadOnlyList<string> Skipped);
