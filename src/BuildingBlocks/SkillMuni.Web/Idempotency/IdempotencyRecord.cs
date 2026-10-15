namespace SkillMuni.Web.Idempotency;

/// <summary>What is kept for one <c>Idempotency-Key</c>: the request fingerprint and, once finished, the response.</summary>
public sealed record IdempotencyRecord(
    string RequestHash,
    bool InProgress,
    int StatusCode = 0,
    string? ContentType = null,
    string? Location = null,
    byte[]? Body = null)
{
    public static IdempotencyRecord Started(string requestHash) => new(requestHash, InProgress: true);
}
