using Asp.Versioning;
using Identity.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace Identity.Api.Controllers;

/// <summary>API-022: the signed-in learner from the session cookie, or 401 no-session.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/session")]
public sealed class SessionController(ISessionService sessions, IOptions<AuthOptions> authOptions) : IdentityControllerBase(authOptions)
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Current(CancellationToken cancellationToken)
    {
        var result = await sessions.CurrentAsync(CurrentAccountId, User.SessionId() ?? Guid.Empty, cancellationToken);
        if (!result.IsSuccess)
        {
            ClearCookies();
            return ApiProblems.Result(HttpContext, result.Error);
        }

        return Ok(result.Value);
    }
}
