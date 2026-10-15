using Asp.Versioning;
using Identity.Application.Contracts;
using Identity.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Idempotency;

namespace Identity.Api.Controllers;

/// <summary>Institute admin: creates the claim link for a CV the institute uploaded. The token is shown once.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/claims")]
[Authorize(Policy = Policies.InstituteAdmin)]
public sealed class AdminClaimsController(IClaimService claims, IOptions<AuthOptions> authOptions) : IdentityControllerBase(authOptions)
{
    [HttpPost]
    [Idempotent]
    public async Task<IActionResult> Create(CreateClaimRequest request, CancellationToken cancellationToken) =>
        (await claims.CreateAsync(request, cancellationToken))
            .ToActionResult(this, created => Created($"/api/v1/claims/{Uri.EscapeDataString(created.Token)}", created));
}
