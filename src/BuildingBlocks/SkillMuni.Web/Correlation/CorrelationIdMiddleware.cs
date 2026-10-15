using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SkillMuni.Common.Tenancy;

namespace SkillMuni.Web.Correlation;

/// <summary>
/// Every request carries <c>X-Correlation-Id</c>. The gateway sets it; services keep it, echo it on the
/// response and put it in the logging scope and on outgoing events.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers[HeaderName].ToString();
        if (!IsValid(id))
        {
            id = Guid.NewGuid().ToString("N");
        }

        context.Items[HeaderName] = id;
        context.Request.Headers[HeaderName] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await next(context);
        }
    }

    public static string? Get(HttpContext context) =>
        context.Items.TryGetValue(HeaderName, out var value) ? value as string : null;

    private static bool IsValid(string id) =>
        id.Length is > 0 and <= MaxLength && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

/// <summary><see cref="ICorrelationContext"/> for code outside the HTTP layer (outbox, typed clients).</summary>
public sealed class HttpCorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext
{
    public string? CorrelationId => accessor.HttpContext is { } http ? CorrelationIdMiddleware.Get(http) : null;
}
