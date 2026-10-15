using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application;
using Platform.Application.Services;
using SkillMuni.Common.Paging;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace Platform.Api.Controllers;

/// <summary>MISS-06: the bell and channel preferences. Not called by the app yet (the bell is a placeholder).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/me")]
[Produces("application/json")]
[Authorize(Policy = Policies.Learner)]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    private Guid LearnerId => User.AccountId() ?? throw new InvalidOperationException("Authorized request without a subject claim.");

    /// <summary>Newest first: <c>{ items, page, pageSize, total, unread }</c>.</summary>
    [HttpGet("notifications")]
    public async Task<ActionResult<NotificationPageDto>> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        await notifications.ListAsync(LearnerId, PageRequest.Create(page, pageSize), cancellationToken);

    /// <summary>204; 404 <c>notification-not-found</c>.</summary>
    [HttpPut("notifications/{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken cancellationToken) =>
        (await notifications.MarkReadAsync(LearnerId, id, cancellationToken)).ToActionResult(this);

    [HttpGet("notification-preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> Preferences(CancellationToken cancellationToken) =>
        await notifications.PreferencesAsync(LearnerId, cancellationToken);

    /// <summary>All four channels: <c>{ push, whatsApp, sms, email }</c>. Codes for sign-in and verification are always sent.</summary>
    [HttpPut("notification-preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> SavePreferences(UpdatePreferencesRequest request, CancellationToken cancellationToken) =>
        await notifications.SavePreferencesAsync(LearnerId, request, cancellationToken);
}
