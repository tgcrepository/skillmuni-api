using Asp.Versioning;
using Gamification.Application;
using Gamification.Application.Services;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Gamification.Api.Controllers;

/// <summary>API-001–003: the Arena.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/arena")]
public sealed class ArenaController(IArenaService arena) : GamificationControllerBase
{
    /// <summary><c>scope</c>: everyone · batch · friends. <c>period</c>: week · month · all (422 validation otherwise).</summary>
    [HttpGet]
    public async Task<IActionResult> Board([FromQuery] ArenaQuery query, CancellationToken cancellationToken) =>
        (await arena.BoardAsync(LearnerId, query.Scope ?? "everyone", query.Period ?? "week", cancellationToken)).ToActionResult(this);

    [HttpPost("players/{id:guid}/cheer")]
    [Idempotent]
    public async Task<IActionResult> Cheer(Guid id, CheerRequest request, CancellationToken cancellationToken) =>
        (await arena.CheerAsync(LearnerId, id, request.On, cancellationToken)).ToActionResult(this);

    /// <summary>204. A friendly one-week XP race; results wait on the Arena rules (MISS-11).</summary>
    [HttpPost("players/{id:guid}/challenge")]
    [Idempotent]
    public async Task<IActionResult> Challenge(Guid id, CancellationToken cancellationToken) =>
        (await arena.ChallengeAsync(LearnerId, id, cancellationToken)).ToActionResult(this);
}
