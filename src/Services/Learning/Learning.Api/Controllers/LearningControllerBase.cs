using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Auth;

namespace Learning.Api.Controllers;

/// <summary>Thin controllers: validate (filter), call a service, map the result. Every action needs a learner session.</summary>
[ApiController]
[Produces("application/json")]
[Authorize(Policy = Policies.Learner)]
public abstract class LearningControllerBase : ControllerBase
{
    /// <summary>Only used on [Authorize] actions, where the token always carries a subject.</summary>
    protected Guid LearnerId => User.AccountId() ?? throw new InvalidOperationException("Authorized request without a subject claim.");
}
