using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using SkillMuni.Common.Security;

namespace SkillMuni.Web.Auth;

public static class RsaKeys
{
    public static RsaSecurityKey Load(string pem, string? keyId = null)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(Pem.Decode(pem));
        return new RsaSecurityKey(rsa) { KeyId = keyId };
    }
}
