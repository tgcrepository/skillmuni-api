using Asp.Versioning;
using Identity.Application.Contracts;
using Identity.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace Identity.Api.Controllers;

/// <summary>API-030/031 (Quest 2 mobile) and Edit profile contact changes (A-273).</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}")]
[Authorize(Policy = Policies.Learner)]
public sealed class ContactController(IContactService contacts, IOptions<AuthOptions> authOptions) : IdentityControllerBase(authOptions)
{
    [HttpPost("me/phone/code")]
    public async Task<IActionResult> SendPhoneCode(SendPhoneCodeRequest request, CancellationToken cancellationToken) =>
        (await contacts.SendPhoneCodeAsync(CurrentAccountId, request, cancellationToken)).ToActionResult(this);

    [HttpPost("me/phone/verify")]
    public async Task<IActionResult> VerifyPhone(VerifyPhoneCodeRequest request, CancellationToken cancellationToken) =>
        (await contacts.VerifyPhoneAsync(CurrentAccountId, request, cancellationToken)).ToActionResult(this);

    [HttpPost("profile/contact/code")]
    public async Task<IActionResult> SendContactCode(ContactCodeRequest request, CancellationToken cancellationToken) =>
        (await contacts.SendContactCodeAsync(CurrentAccountId, request, cancellationToken)).ToActionResult(this);

    [HttpPost("profile/contact/verify")]
    public async Task<IActionResult> VerifyContact(ContactVerifyRequest request, CancellationToken cancellationToken) =>
        (await contacts.VerifyContactAsync(CurrentAccountId, request, cancellationToken)).ToActionResult(this);
}
