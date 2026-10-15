using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SkillMuni.Common.Security;

namespace SkillMuni.Testing;

/// <summary>Mints access tokens the way Identity does, with a throwaway key, for testing a service on its own.</summary>
public sealed class TestTokens : IDisposable
{
    public const string Tenant = "skillmuni-in";

    private readonly RSA _key = RSA.Create(2048);
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>Goes in the service's <c>Auth:SigningPublicKeyPem</c>.</summary>
    public string PublicKeyPem => _key.ExportSubjectPublicKeyInfoPem();

    public string Create(Guid accountId, string firstName = "Asha", string[]? roles = null, string tenant = Tenant, string market = "IN")
    {
        var now = DateTime.UtcNow;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "skillmuni-identity",
            Audience = "skillmuni-api",
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_key), SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                [SkillMuniClaims.Subject] = accountId.ToString(),
                [SkillMuniClaims.Session] = Guid.NewGuid().ToString(),
                [SkillMuniClaims.Tenant] = tenant,
                [SkillMuniClaims.Market] = market,
                [SkillMuniClaims.Name] = firstName,
                [SkillMuniClaims.Role] = roles ?? ["learner"],
            },
        });
    }

    public void Dispose() => _key.Dispose();
}
