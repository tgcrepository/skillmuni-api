using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using SkillMuni.Common.Results;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace SkillMuni.Gateway;

/// <summary>
/// Per-account limit (CLAUDE.md §6), after authentication: one signed-in learner can't flood the services from many
/// IPs. The per-IP limits run earlier, before the token is read.
/// </summary>
public sealed class AccountRateLimitMiddleware(RequestDelegate next, IOptions<GatewayOptions> options) : IAsyncDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(account =>
        RateLimitPartition.GetFixedWindowLimiter(account, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = options.Value.AccountPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.AccountId() is not { } account)
        {
            await next(context);
            return;
        }

        using var lease = await _limiter.AcquireAsync(account.ToString("N"), 1, context.RequestAborted);
        if (!lease.IsAcquired)
        {
            context.Response.Headers.RetryAfter = "60";
            await ApiProblems.WriteAsync(
                context,
                Error.RateLimited("too-many-requests", "Too many requests. Try again in a minute."),
                context.RequestAborted);
            return;
        }

        await next(context);
    }

    public ValueTask DisposeAsync() => _limiter.DisposeAsync();
}
