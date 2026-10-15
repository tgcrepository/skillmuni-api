using Identity.Application.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Results;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace Identity.Api.Controllers;

/// <summary>Thin controllers: validate (filter), call a service, map the result. Sessions travel in cookies.</summary>
[ApiController]
[Produces("application/json")]
public abstract class IdentityControllerBase(IOptions<AuthOptions> authOptions) : ControllerBase
{
    protected AuthOptions Auth => authOptions.Value;

    /// <summary>Only used on [Authorize] actions, where the token always carries a subject.</summary>
    protected Guid CurrentAccountId =>
        User.AccountId() ?? throw new InvalidOperationException("Authorized request without a subject claim.");

    /// <summary>200 with the body, plus the session cookies.</summary>
    protected IActionResult SignedIn<T>(Result<SignedIn<T>> result) =>
        result.ToActionResult(this, signedIn =>
        {
            WriteCookies(signedIn.Tokens);
            return Ok(signedIn.Body);
        });

    protected void WriteCookies(SessionTokens tokens) =>
        SessionCookies.Append(Response, Auth, tokens.AccessToken, tokens.AccessExpiresAt, tokens.RefreshToken, tokens.RefreshExpiresAt);

    protected void ClearCookies() => SessionCookies.Delete(Response, Auth);

    protected string? RefreshCookie =>
        Request.Cookies.TryGetValue(Auth.RefreshCookieName, out var token) && !string.IsNullOrEmpty(token) ? token : null;
}
