using Asp.Versioning;
using Identity.Application.Contracts;
using Identity.Application.Errors;
using Identity.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace Identity.Api.Controllers;

/// <summary>Login, sign-up, verification and sign-out (API-011 to API-021, BR-136, A-159, A-253, A-308).</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
[AllowAnonymous]
public sealed class AuthController(
    ILoginService login,
    IRegistrationService registration,
    IGoogleSignInService google,
    ISessionService sessions,
    IOptions<AuthOptions> authOptions) : IdentityControllerBase(authOptions)
{
    /// <summary>Send a login code to a registered mobile. 404 account-not-found → sign-up; 423 locked.</summary>
    [HttpPost("otp")]
    public async Task<IActionResult> SendOtp(SendPhoneCodeRequest request, CancellationToken cancellationToken) =>
        (await login.SendPhoneCodeAsync(request, cancellationToken)).ToActionResult(this);

    /// <summary>422 invalid-code (with triesLeft) · 410 code-expired · 429 too-many-tries.</summary>
    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp(VerifyPhoneCodeRequest request, CancellationToken cancellationToken) =>
        SignedIn(await login.VerifyPhoneCodeAsync(request, cancellationToken));

    [HttpPost("email/login-code")]
    public async Task<IActionResult> SendEmailLoginCode(SendEmailCodeRequest request, CancellationToken cancellationToken) =>
        (await login.SendEmailCodeAsync(request, cancellationToken)).ToActionResult(this);

    [HttpPost("email/login-verify")]
    public async Task<IActionResult> VerifyEmailLogin(VerifyEmailCodeRequest request, CancellationToken cancellationToken) =>
        SignedIn(await login.VerifyEmailCodeAsync(request, cancellationToken));

    /// <summary>401 wrong-credentials · 423 account-locked.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> LoginWithPassword(PasswordLoginRequest request, CancellationToken cancellationToken) =>
        SignedIn(await login.PasswordLoginAsync(request, cancellationToken));

    [HttpPost("password-reset")]
    public async Task<IActionResult> RequestPasswordReset(PasswordResetRequest request, CancellationToken cancellationToken) =>
        Ok(await login.RequestPasswordResetAsync(request, cancellationToken));

    /// <summary>New: completes a reset from the emailed link; signs out every device.</summary>
    [HttpPost("password-reset/confirm")]
    public async Task<IActionResult> ConfirmPasswordReset(PasswordResetConfirmRequest request, CancellationToken cancellationToken) =>
        (await login.ConfirmPasswordResetAsync(request, cancellationToken)).ToActionResult(this);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessions.LogoutAsync(User.SessionId(), RefreshCookie, cancellationToken);
        ClearCookies();
        return NoContent();
    }

    /// <summary>"Join the squad". 409 email-taken · 422 consent-required.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken) =>
        (await registration.RegisterAsync(request, cancellationToken)).ToActionResult(this);

    [HttpPost("email/resend")]
    public async Task<IActionResult> ResendEmailCode(SendEmailCodeRequest request, CancellationToken cancellationToken) =>
        (await registration.ResendAsync(request, cancellationToken)).ToActionResult(this);

    [HttpPost("email/verify")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailCodeRequest request, CancellationToken cancellationToken) =>
        SignedIn(await registration.VerifyAsync(request, cancellationToken));

    /// <summary>A-253: <c>isNew</c> = this Google account just created a learner.</summary>
    [HttpPost("google")]
    public async Task<IActionResult> GoogleSignIn(GoogleSignInRequest request, CancellationToken cancellationToken) =>
        SignedIn(await google.SignInAsync(request, cancellationToken));

    /// <summary>MISS-13: explicit refresh (the gateway normally does this silently). 204 + new cookies.</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (RefreshCookie is not { } token)
        {
            return ApiProblems.Result(HttpContext, IdentityErrors.NoSession);
        }

        var result = await sessions.RefreshAsync(token, cancellationToken);
        if (!result.IsSuccess)
        {
            ClearCookies();
            return ApiProblems.Result(HttpContext, result.Error);
        }

        WriteCookies(result.Value);
        return NoContent();
    }
}
