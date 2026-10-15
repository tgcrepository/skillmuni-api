using Identity.Domain.Accounts;
using Identity.Domain.Sessions;

namespace Identity.Application.Abstractions;

/// <summary>Keyed hash (HMAC) for codes, refresh tokens and link tokens; only hashes are stored.</summary>
public interface ISecretHasher
{
    string Hash(string secret);

    bool Matches(string secret, string hash);
}

public interface ICodeGenerator
{
    /// <summary>A 6-digit one-time code.</summary>
    string NewCode();

    /// <summary>A URL-safe random token (refresh tokens, links).</summary>
    string NewToken();
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}

public sealed record AccessToken(string Value, DateTime ExpiresAt);

/// <summary>Signs the short-lived access JWT.</summary>
public interface ITokenIssuer
{
    AccessToken Issue(Account account, Session session, DateTime now);
}

public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? GivenName, string? FamilyName);

public interface IGoogleTokenValidator
{
    /// <summary>Validates a Google ID token for this client id; null when it is invalid.</summary>
    Task<GoogleIdentity?> ValidateAsync(string credential, string clientId, CancellationToken cancellationToken);
}

/// <summary>Per-tenant settings the current request needs.</summary>
public interface ITenantSettings
{
    string? GoogleClientId { get; }
}

/// <summary>Who is calling, from the access token.</summary>
public interface ICurrentUser
{
    Guid? AccountId { get; }

    Guid? SessionId { get; }

    string? UserAgent { get; }
}
