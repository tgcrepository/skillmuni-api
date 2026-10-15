using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Common.Security;
using SkillMuni.Web.Auth;

namespace Careers.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Authorize(Policy = Policies.Learner)]
public abstract class CareersControllerBase : ControllerBase
{
    protected Guid LearnerId => User.AccountId() ?? throw new InvalidOperationException("Authorized request without a subject claim.");

    /// <summary>The name in the session token (first name), used where Learner can't give the full name.</summary>
    protected string DisplayName => User.FindFirst(SkillMuniClaims.Name)?.Value ?? string.Empty;
}
