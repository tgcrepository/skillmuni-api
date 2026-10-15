using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Auth;

namespace Gamification.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Authorize(Policy = Policies.Learner)]
public abstract class GamificationControllerBase : ControllerBase
{
    protected Guid LearnerId => User.AccountId() ?? throw new InvalidOperationException("Authorized request without a subject claim.");
}
