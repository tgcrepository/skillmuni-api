using System.Security.Cryptography;
using System.Text;
using Identity.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Security;

/// <summary>HMAC-SHA256 with a Key Vault key: a stolen table of hashes can't be reversed without the key.</summary>
public sealed class HmacSecretHasher : ISecretHasher
{
    private const int MinKeyBytes = 32;
    private readonly byte[] _key;

    public HmacSecretHasher(IOptions<SecurityOptions> options)
    {
        _key = Convert.FromBase64String(options.Value.HashKey);
        if (_key.Length < MinKeyBytes)
        {
            throw new InvalidOperationException($"Identity:Security:HashKey must be at least {MinKeyBytes} bytes.");
        }
    }

    public string Hash(string secret) => Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(secret)));

    public bool Matches(string secret, string hash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(secret)), Encoding.ASCII.GetBytes(hash));
}
