using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SkillMuni.Web.Correlation;

namespace SkillMuni.Web.Errors;

/// <summary>The one place unexpected exceptions become a response (CLAUDE.md §7).</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        if (exception is BadHttpRequestException bad)
        {
            var problem = ApiProblems.Create(httpContext, bad.StatusCode, "malformed-request", "The request could not be read.");
            await ApiProblems.WriteAsync(httpContext, problem, cancellationToken);
            return true;
        }

        logger.LogError(exception, "Unhandled exception {CorrelationId}", CorrelationIdMiddleware.Get(httpContext));
        await ApiProblems.WriteServerErrorAsync(httpContext, cancellationToken);
        return true;
    }
}
