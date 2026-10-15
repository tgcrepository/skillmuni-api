namespace Identity.Infrastructure.Security;

/// <summary>Secrets for hashing and signing. In Azure every value comes from Key Vault.</summary>
public sealed class SecurityOptions
{
    public const string Section = "Identity:Security";

    /// <summary>Base64 key (32+ bytes) for the HMAC that protects stored codes and tokens.</summary>
    public string HashKey { get; set; } = string.Empty;

    /// <summary>
    /// Local development and automated tests only: every code is this value (the mock uses 123456). Start-up
    /// fails if it is set in any other environment.
    /// </summary>
    public string? DevelopmentFixedCode { get; set; }
}

/// <summary>Access-token signing. Bound from <c>Auth</c>, the section the validators read too.</summary>
public sealed class TokenSigningOptions
{
    public string Issuer { get; set; } = "skillmuni-identity";

    public string Audience { get; set; } = "skillmuni-api";

    /// <summary>RSA private key (PEM or base64 PEM). Identity only.</summary>
    public string SigningPrivateKeyPem { get; set; } = string.Empty;

    public string KeyId { get; set; } = "identity-1";
}
