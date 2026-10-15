using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Web.Auth;

namespace Assessment.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}")]
[Produces("application/json")]
[Authorize(Policy = Policies.Learner)]
public abstract class AssessmentControllerBase : ControllerBase
{
    protected Guid LearnerId => User.AccountId() ?? throw new InvalidOperationException("Authorized request without a subject claim.");
}
