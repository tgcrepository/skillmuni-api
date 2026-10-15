using Asp.Versioning;
using Learning.Application.Contracts;
using Learning.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Learning.Api.Controllers;

/// <summary>API-097…103 (learn.api.ts) and MISS-04 learning minutes.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/learn")]
public sealed class LearnController(ILearnService learn) : LearningControllerBase
{
    /// <summary>API-097: a module with its topics, progress and (B-44) where to resume.</summary>
    [HttpGet("modules/{module}")]
    [ProducesResponseType<ModuleDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Module(string module, CancellationToken cancellationToken) =>
        (await learn.ModuleAsync(LearnerId, module, cancellationToken)).ToActionResult(this);

    /// <summary>API-098: a topic's briefs with status, bookmark, rating and last quiz score.</summary>
    [HttpGet("modules/{module}/topics/{topic}")]
    [ProducesResponseType<TopicDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Topic(string module, string topic, CancellationToken cancellationToken) =>
        (await learn.TopicAsync(LearnerId, module, topic, cancellationToken)).ToActionResult(this);

    /// <summary>API-099.</summary>
    [HttpPut("briefs/{id}/bookmark")]
    public async Task<IActionResult> Bookmark(string id, BookmarkRequest request, CancellationToken cancellationToken) =>
        (await learn.BookmarkAsync(LearnerId, id, request.On ?? false, cancellationToken)).ToActionResult(this);

    /// <summary>API-100: <c>helpful</c> true, false or null (cleared).</summary>
    [HttpPut("briefs/{id}/feedback")]
    public async Task<IActionResult> Feedback(string id, BriefFeedbackRequest request, CancellationToken cancellationToken) =>
        (await learn.FeedbackAsync(LearnerId, id, request.Helpful, cancellationToken)).ToActionResult(this);

    /// <summary>API-101: the quiz of the topic's current brief (or the last brief, as practice, once all are done).</summary>
    [HttpGet("modules/{module}/topics/{topic}/quiz")]
    [ProducesResponseType<QuizDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Quiz(string module, string topic, CancellationToken cancellationToken) =>
        (await learn.QuizAsync(LearnerId, module, topic, cancellationToken)).ToActionResult(this);

    /// <summary>API-102: autosaved picks.</summary>
    [HttpPut("quizzes/{id}/draft")]
    public async Task<IActionResult> SaveDraft(string id, QuizAnswersRequest request, CancellationToken cancellationToken) =>
        (await learn.SaveDraftAsync(LearnerId, id, request.Answers!, cancellationToken)).ToActionResult(this);

    /// <summary>API-103: check the answers. +1 XP per correct answer on the first submit only (BR-106).</summary>
    [HttpPost("quizzes/{id}/submit")]
    [Idempotent]
    [ProducesResponseType<QuizResultDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(string id, QuizAnswersRequest request, CancellationToken cancellationToken) =>
        (await learn.SubmitAsync(LearnerId, id, request.Answers!, cancellationToken)).ToActionResult(this);

    /// <summary>MISS-04: learning minutes (1–60 per call). Not called by the app yet.</summary>
    [HttpPost("activity")]
    [Idempotent]
    public async Task<IActionResult> Activity(ActivityRequest request, CancellationToken cancellationToken) =>
        (await learn.RecordActivityAsync(LearnerId, request.Minutes!.Value, cancellationToken)).ToActionResult(this);
}
