using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SkillMuni.Common.Privacy;
using SkillMuni.Web.Errors;

namespace SkillMuni.Web.Auth;

/// <summary>
/// MISS-08: an access token stays valid for up to 10 minutes after the learner erases their account. In a service
/// that keeps erasure tombstones (<see cref="IErasureTombstones"/> registered), every request from an erased account
/// gets 410 <c>learner-erased</c>, so nothing rebuilds the learner as an empty profile. Services without tombstones
/// pass straight through.
/// </summary>
public sealed class ErasedAccountMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.AccountId() is { } accountId
            && context.RequestServices.GetService<IErasureTombstones>() is { } tombstones
            && await tombstones.IsErasedAsync(accountId, context.RequestAborted))
        {
            await ApiProblems.WriteAsync(context, ErasureErrors.LearnerErased, context.RequestAborted);
            return;
        }

        await next(context);
    }
}
