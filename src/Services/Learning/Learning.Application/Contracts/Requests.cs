namespace Learning.Application.Contracts;

/// <summary>API-099: <c>{ on }</c>.</summary>
public sealed record BookmarkRequest(bool? On);

/// <summary>API-100: <c>{ helpful }</c>; null clears the rating.</summary>
public sealed record BriefFeedbackRequest(bool? Helpful);

/// <summary>API-102 / API-103: question id → picked option id.</summary>
public sealed record QuizAnswersRequest(Dictionary<string, string>? Answers);

/// <summary>MISS-04: learning minutes since the last report, 1–60.</summary>
public sealed record ActivityRequest(int? Minutes);
