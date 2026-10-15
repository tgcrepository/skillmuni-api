using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using SkillMuni.Common.Results;
using SkillMuni.Common.Tenancy;
using SkillMuni.Web.Auth;
using SkillMuni.Web.Errors;

namespace SkillMuni.Web.Idempotency;

/// <summary>
/// Replays the first response for a repeated <c>Idempotency-Key</c> (CLAUDE.md §4, BR-116).
/// Keys are scoped to tenant, caller, method and path, and kept for 24 hours in the distributed cache (Redis).
/// 2xx and 4xx responses are kept; 5xx, 409 and 429 are not, so the client can retry those.
/// </summary>
/// <remarks>
/// The XP ledger's unique key and each service's own constraints remain the final guard: two first requests that
/// arrive within the same instant can both pass the "in progress" check.
/// </remarks>
public sealed partial class IdempotencyFilter(
    IDistributedCache cache,
    ITenantContext tenant,
    ILogger<IdempotencyFilter> logger) : IAsyncResourceFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";
    public const int MaxKeyLength = 64;

    private const long MaxHashedBodyBytes = 1024 * 1024;
    private static readonly TimeSpan InProgressFor = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan KeptFor = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var cancellationToken = http.RequestAborted;
        if (!http.Request.Headers.TryGetValue(HeaderName, out var values) || string.IsNullOrWhiteSpace(values.ToString()))
        {
            await next();
            return;
        }

        var key = values.ToString().Trim();
        if (!IsValidKey(key))
        {
            context.Result = ApiProblems.ToResult(ApiProblems.Create(
                http,
                StatusCodes.Status400BadRequest,
                "invalid-idempotency-key",
                $"The Idempotency-Key header must be 1 to {MaxKeyLength} letters, digits or - _ . :"));
            return;
        }

        var cacheKey = CacheKey(http, key);
        var requestHash = await HashRequestAsync(http.Request, cancellationToken);
        var existing = await ReadAsync(cacheKey, cancellationToken);
        if (existing is not null)
        {
            context.Result = Replay(http, existing, requestHash);
            return;
        }

        await WriteAsync(cacheKey, IdempotencyRecord.Started(requestHash), InProgressFor, cancellationToken);

        var response = http.Response;
        var originalBody = response.Body;
        using var buffer = new MemoryStream();
        response.Body = buffer;
        ResourceExecutedContext executed;
        try
        {
            executed = await next();
        }
        finally
        {
            response.Body = originalBody;
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody, cancellationToken);

        if ((executed.Exception is not null && !executed.ExceptionHandled) || !IsKept(response.StatusCode))
        {
            await cache.RemoveAsync(cacheKey, cancellationToken);
            return;
        }

        var record = new IdempotencyRecord(
            requestHash,
            InProgress: false,
            response.StatusCode,
            response.ContentType,
            response.Headers.Location.ToString() is { Length: > 0 } location ? location : null,
            buffer.ToArray());
        await WriteAsync(cacheKey, record, KeptFor, cancellationToken);
    }

    private IActionResult Replay(HttpContext http, IdempotencyRecord existing, string requestHash)
    {
        if (existing.InProgress)
        {
            return ApiProblems.Result(http, Error.Conflict("request-in-progress", "This request is still being processed. Try again in a moment."));
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return ApiProblems.Result(
                http,
                Error.Validation("idempotency-key-reused", "This Idempotency-Key was already used for a different request."));
        }

        LogReplayed(logger, http.Request.Method, http.Request.Path.Value ?? string.Empty, existing.StatusCode);
        return new ReplayedResponseResult(existing);
    }

    internal static bool IsValidKey(string key) =>
        key.Length is > 0 and <= MaxKeyLength
        && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');

    /// <summary>Success and client errors are final answers; server errors, conflicts and throttling may change on retry.</summary>
    internal static bool IsKept(int status) =>
        status is >= 200 and < 300 || (status is >= 400 and < 500 && status is not 409 and not 429);

    private string CacheKey(HttpContext http, string key)
    {
        var caller = http.User.AccountId()?.ToString("N") ?? "anonymous";
        return $"idempotency:{tenant.TenantId}:{caller}:{http.Request.Method}:{http.Request.Path.Value?.ToLowerInvariant()}:{key}";
    }

    /// <summary>SHA-256 of the body, so a key reused with a different body is refused. Large uploads hash their size and type.</summary>
    private static async Task<string> HashRequestAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is null or > MaxHashedBodyBytes)
        {
            return $"{request.ContentType}|{request.ContentLength}";
        }

        request.EnableBuffering();
        var hash = await SHA256.HashDataAsync(request.Body, cancellationToken);
        request.Body.Position = 0;
        return Convert.ToHexString(hash);
    }

    private async Task<IdempotencyRecord?> ReadAsync(string cacheKey, CancellationToken cancellationToken)
    {
        var bytes = await cache.GetAsync(cacheKey, cancellationToken);
        return bytes is null ? null : JsonSerializer.Deserialize<IdempotencyRecord>(bytes, Json);
    }

    private Task WriteAsync(string cacheKey, IdempotencyRecord record, TimeSpan keptFor, CancellationToken cancellationToken) =>
        cache.SetAsync(
            cacheKey,
            JsonSerializer.SerializeToUtf8Bytes(record, Json),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = keptFor },
            cancellationToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "Idempotent replay {Method} {Path} {StatusCode}")]
    private static partial void LogReplayed(ILogger logger, string method, string path, int statusCode);

    /// <summary>Writes a stored response back as it was first sent.</summary>
    private sealed class ReplayedResponseResult(IdempotencyRecord record) : IActionResult
    {
        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.StatusCode = record.StatusCode;
            response.Headers[ReplayedHeaderName] = "true";
            if (record.ContentType is { } contentType)
            {
                response.ContentType = contentType;
            }

            if (record.Location is { } location)
            {
                response.Headers.Location = location;
            }

            if (record.Body is { Length: > 0 } body)
            {
                await response.Body.WriteAsync(body, context.HttpContext.RequestAborted);
            }
        }
    }
}
