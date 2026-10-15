namespace SkillMuni.Common.Results;

/// <summary>What went wrong, in terms the HTTP layer can map to a status code.</summary>
public enum ErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Gone,
    Locked,
    RateLimited,
    Unavailable,

    /// <summary>An upstream the request depends on didn't answer in time (504).</summary>
    Timeout,
}

/// <summary>
/// An expected failure. <see cref="Code"/> is the business code the frontend reads from
/// <c>errors.code[0]</c>; <see cref="Details"/> holds extra field entries (for example <c>triesLeft</c>).
/// </summary>
public sealed record Error(ErrorKind Kind, string Code, string Message)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoDetails = new Dictionary<string, string[]>();

    public IReadOnlyDictionary<string, string[]> Details { get; init; } = NoDetails;

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(ErrorKind.Validation, code, message) { Details = details ?? NoDetails };

    public static Error Unauthorized(string code, string message) => new(ErrorKind.Unauthorized, code, message);

    public static Error Forbidden(string code, string message) => new(ErrorKind.Forbidden, code, message);

    public static Error NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);

    public static Error Conflict(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(ErrorKind.Conflict, code, message) { Details = details ?? NoDetails };

    public static Error Gone(string code, string message) => new(ErrorKind.Gone, code, message);

    public static Error Locked(string code, string message) => new(ErrorKind.Locked, code, message);

    public static Error RateLimited(string code, string message) => new(ErrorKind.RateLimited, code, message);

    public static Error Unavailable(string code, string message) => new(ErrorKind.Unavailable, code, message);

    public static Error Timeout(string code, string message) => new(ErrorKind.Timeout, code, message);
}
