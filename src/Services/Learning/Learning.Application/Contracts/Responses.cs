using System.Text.Json.Serialization;

namespace Learning.Application.Contracts;

// JSON shapes of the app's learn.api.ts, home.api.ts and stats.api.ts. Optional TypeScript fields (`example?`) are
// left out when empty, as the mock does; `T | null` fields are written as null.

public sealed record SlugTitleDto(string Slug, string Title);

public sealed record TitleDto(string Title);

/* ---------- Module (API-097) ---------- */

public sealed record TopicSummaryDto(string Slug, string Title, string Art, int QuizzesDone, int QuizzesTotal, int Xp);

/// <summary>B-44: the next unfinished brief, so "Finish Brain-Vita" and the app can deep-link where the learner left off.</summary>
public sealed record ModuleResumeDto(SlugTitleDto Topic, ResumeBriefDto Brief, string Link);

public sealed record ResumeBriefDto(string Id, int Index, string Title);

public sealed record ModuleDetailDto(
    string Slug,
    string Title,
    string Tagline,
    SlugTitleDto Knook,
    int QuizzesDone,
    int QuizzesTotal,
    int Xp,
    IReadOnlyList<TopicSummaryDto> Topics,
    ModuleResumeDto? Resume);

/* ---------- Topic (API-098) ---------- */

public sealed record BriefMediaDto(
    string Kind,
    string Src,
    string Alt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Poster,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Duration);

public sealed record QuizScoreDto(int Correct, int Total);

public sealed record BriefQuizDto(string Id, int Questions, QuizScoreDto? Result);

public sealed record BriefDto(
    string Id,
    int Index,
    string Title,
    string Status,
    string Body,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Example,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? KeyTerm,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? VideoUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BriefMediaDto? Media,
    bool Bookmarked,
    bool? Helpful,
    int Xp,
    BriefQuizDto Quiz);

public sealed record CourseCardDto(string Title, bool Unlocked, string Link);

public sealed record TopicDetailDto(
    string Slug,
    string Title,
    SlugTitleDto Module,
    TitleDto Knook,
    int QuizzesDone,
    int QuizzesTotal,
    int Xp,
    IReadOnlyList<BriefDto> Briefs,
    CourseCardDto? Course);

/* ---------- Quiz (API-101, API-103) ---------- */

public sealed record QuizOptionDto(string Id, string Text);

public sealed record QuizQuestionDto(string Id, string Prompt, IReadOnlyList<QuizOptionDto> Options);

public sealed record QuizDto(
    string Id,
    string Title,
    SlugTitleDto Module,
    SlugTitleDto Topic,
    TitleDto Knook,
    int QuizzesDone,
    int QuizzesTotal,
    int Xp,
    IReadOnlyList<QuizQuestionDto> Questions,
    IReadOnlyDictionary<string, string> Draft,
    bool AlreadyPassed);

public sealed record QuestionResultDto(
    string QuestionId,
    string CorrectOptionId,
    string PickedOptionId,
    bool Correct,
    string Explanation,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PickNote);

public sealed record QuizResultDto(int Correct, int Total, int Xp, IReadOnlyList<QuestionResultDto> Results, string Muni);

/* ---------- Lobby (API-061…063) ---------- */

public sealed record ResumeCardDto(string Module, string Topic, int Brief, int Briefs, string Note, int Percent, int Xp, string Art, string Link);

public sealed record PathwayDto(string Slug, string Title, string Blurb, string Tone, string Art, string? Link);

public sealed record KnookModuleDto(string Slug, string Title, string Art, string Link);

public sealed record KnookDto(string Slug, string Title, string Icon, bool Expanded, IReadOnlyList<KnookModuleDto> Modules);

public sealed record FirstStepDto(string Id, string Title, string Meta, int? Xp, string Link, bool Done);

public sealed record FirstMovesBonusDto(int Xp, bool Claimed);

public sealed record HomeFeedDto(
    string Daypart,
    ResumeCardDto? Resume,
    IReadOnlyList<FirstStepDto>? FirstSteps,
    FirstMovesBonusDto? FirstMovesBonus,
    IReadOnlyList<PathwayDto> Pathways,
    IReadOnlyList<KnookDto> Knooks,
    int RewardsReady,
    int UnreadNotifications);

public sealed record FirstMovesClaimDto(int Xp);

/* ---------- My Stats · Learning (API-130) ---------- */

public sealed record LearningCourseDto(
    string Id,
    string Title,
    string Tone,
    double Percent,
    string Status,
    string Kind,
    int XpOnFinish,
    int? DeadlineDays,
    double? TimeEstHours,
    double? CpdHours,
    string Summary,
    string Link);

public sealed record LearningDashboardDto(IReadOnlyList<LearningCourseDto> Courses, string? CurrentId, string? Nudge);
