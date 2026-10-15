using Learner.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Common.Security;
using SkillMuni.Web.Auth;

namespace Learner.Api.Controllers;

/// <summary>Thin controllers: validate (filter), call a service, map the result. Every action needs a learner session.</summary>
[ApiController]
[Produces("application/json")]
[Authorize(Policy = Policies.Learner)]
public abstract class LearnerControllerBase : ControllerBase
{
    /// <summary>Only used on [Authorize] actions, where the token always carries a subject.</summary>
    protected Guid CurrentLearnerId =>
        User.AccountId() ?? throw new InvalidOperationException("Authorized request without a subject claim.");

    /// <summary>The caller as the token describes them (used to create the learner if the sign-up event is late).</summary>
    protected LearnerRef CurrentLearner => new(
        CurrentLearnerId,
        User.FindFirst(SkillMuniClaims.Name)?.Value ?? string.Empty,
        User.FindFirst(SkillMuniClaims.Market)?.Value ?? "IN");

    protected static UploadedFile? ToUpload(IFormFile? file) =>
        file is null ? null : new UploadedFile(file.FileName, file.ContentType, file.Length, file.OpenReadStream());
}
