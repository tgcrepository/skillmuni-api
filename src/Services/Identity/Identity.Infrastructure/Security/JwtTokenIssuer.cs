using System.Security.Cryptography;
using Identity.Application;
using Identity.Application.Abstractions;
using Identity.Domain.Accounts;
using Identity.Domain.Sessions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SkillMuni.Common.Security;

namespace Identity.Infrastructure.Security;

/// <summary>RS256 access tokens. Services hold only the public key, so only Identity can mint a session.</summary>
public sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };
    private readonly SigningCredentials _credentials;
    private readonly TokenSigningOptions _signing;
    private readonly TimeSpan _lifetime;

    public JwtTokenIssuer(IOptions<TokenSigningOptions> signing, IOptions<IdentityServiceOptions> rules)
    {
        _signing = signing.Value;
        _lifetime = rules.Value.AccessTokenLifetime;
        var rsa = RSA.Create();
        rsa.ImportFromPem(Pem.Decode(_signing.SigningPrivateKeyPem));
        _credentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = _signing.KeyId }, SecurityAlgorithms.RsaSha256);
    }

    public AccessToken Issue(Account account, Session session, DateTime now)
    {
        var expires = now + _lifetime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _signing.Issuer,
            Audience = _signing.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [SkillMuniClaims.Subject] = account.Id.ToString(),
                [SkillMuniClaims.Session] = session.Id.ToString(),
                [SkillMuniClaims.Tenant] = account.TenantId,
                [SkillMuniClaims.Market] = account.Market,
                [SkillMuniClaims.Name] = account.FirstName,
                [SkillMuniClaims.Role] = account.RoleList.ToArray(),
            },
        };
        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }
}
