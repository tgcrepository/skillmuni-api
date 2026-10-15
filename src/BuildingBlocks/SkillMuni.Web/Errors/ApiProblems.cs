using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillMuni.Common.Results;
using SkillMuni.Web.Correlation;

namespace SkillMuni.Web.Errors;

/// <summary>
/// Builds the RFC 7807 body the frontend reads: <c>message</c> (one sentence) and
/// <c>errors</c> (<c>{ "code": ["account-not-found"], "&lt;field&gt;": ["…"] }</c>), plus the correlation id.
/// </summary>
public static class ApiProblems
{
    public const string ContentType = "application/problem+json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static int StatusFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Gone => StatusCodes.Status410Gone,
        ErrorKind.Locked => StatusCodes.Status423Locked,
        ErrorKind.RateLimited => StatusCodes.Status429TooManyRequests,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorKind.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static ProblemDetails Create(
        HttpContext http,
        int status,
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? fields = null)
    {
        var errors = fields is null ? new Dictionary<string, string[]>() : new Dictionary<string, string[]>(fields);
        errors["code"] = [code];

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrase(status),
            Type = $"https://httpstatuses.io/{status}",
        };
        problem.Extensions["message"] = message;
        problem.Extensions["errors"] = errors;
        problem.Extensions["correlationId"] = CorrelationIdMiddleware.Get(http);
        return problem;
    }

    public static ProblemDetails From(HttpContext http, Error error) =>
        Create(http, StatusFor(error.Kind), error.Code, error.Message, error.Details);

    /// <summary>An MVC result for an expected failure.</summary>
    public static IActionResult Result(HttpContext http, Error error) => ToResult(From(http, error));

    /// <remarks>
    /// Written directly rather than as an <see cref="ObjectResult"/>: a controller's <c>[Produces("application/json")]</c>
    /// filter rewrites an ObjectResult's content types, and the frontend expects <c>application/problem+json</c>.
    /// </remarks>
    public static IActionResult ToResult(ProblemDetails problem) => new ProblemActionResult(problem);

    /// <summary>Writes the problem straight to the response (middleware, auth events).</summary>
    public static Task WriteAsync(HttpContext http, Error error, CancellationToken cancellationToken = default) =>
        WriteAsync(http, From(http, error), cancellationToken);

    public static Task WriteAsync(HttpContext http, ProblemDetails problem, CancellationToken cancellationToken = default)
    {
        http.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return http.Response.WriteAsJsonAsync(problem, Json, ContentType, cancellationToken);
    }

    /// <summary>
    /// The body for an error status nothing else wrote (unknown route, wrong verb, unsupported media type), so every
    /// error the app sees is problem+json with <c>message</c> and <c>errors.code</c>. Used with <c>UseStatusCodePages</c>.
    /// </summary>
    public static Task WriteStatusAsync(HttpContext http, CancellationToken cancellationToken = default)
    {
        var status = http.Response.StatusCode;
        var (code, message) = status switch
        {
            StatusCodes.Status404NotFound => ("not-found", "Not found."),
            StatusCodes.Status405MethodNotAllowed => ("method-not-allowed", "This method isn't allowed here."),
            StatusCodes.Status415UnsupportedMediaType => ("unsupported-media-type", "Send the request as JSON."),
            >= StatusCodes.Status500InternalServerError => ("server-error", "Something went wrong"),
            _ => ("error", "The request could not be completed."),
        };
        return WriteAsync(http, Create(http, status, code, message), cancellationToken);
    }

    /// <summary>5xx: only "Something went wrong" and the correlation id (CLAUDE.md §7).</summary>
    public static Task WriteServerErrorAsync(HttpContext http, CancellationToken cancellationToken = default) =>
        WriteAsync(
            http,
            Create(http, StatusCodes.Status500InternalServerError, "server-error", "Something went wrong"),
            cancellationToken);

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        410 => "Gone",
        422 => "Unprocessable Entity",
        423 => "Locked",
        429 => "Too Many Requests",
        503 => "Service Unavailable",
        _ => "Internal Server Error",
    };
}

/// <summary>Writes a <see cref="ProblemDetails"/> as <c>application/problem+json</c>, untouched by content negotiation.</summary>
internal sealed class ProblemActionResult(ProblemDetails problem) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context) =>
        ApiProblems.WriteAsync(context.HttpContext, problem, context.HttpContext.RequestAborted);
}
