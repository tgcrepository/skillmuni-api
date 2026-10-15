using System.Security.Cryptography;
using System.Text;
using Learner.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Learner.Infrastructure.Security;

/// <summary><c>Learner:Security</c>. The key is a Key Vault secret (base64, 32 bytes), never in config files.</summary>
public sealed class LearnerSecurityOptions
{
    public const string Section = "Learner:Security";

    public string EncryptionKey { get; set; } = string.Empty;

    public static bool IsValidKey(string key)
    {
        try
        {
            return Convert.FromBase64String(key).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>
/// AES-256-GCM: <c>v1:</c> + base64(nonce ‖ tag ‖ ciphertext). The version prefix lets the key be rotated later
/// (decrypt with the old key, re-encrypt with the new) without guessing which rows are which.
/// </summary>
internal sealed class AesGcmFieldEncryptor(IOptions<LearnerSecurityOptions> options) : IFieldEncryptor
{
    private const string Prefix = "v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key = Convert.FromBase64String(options.Value.EncryptionKey);

    public string Encrypt(string plaintext)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var output = new byte[NonceSize + TagSize + plain.Length];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, output.AsSpan(NonceSize + TagSize), output.AsSpan(NonceSize, TagSize));
        return Prefix + Convert.ToBase64String(output);
    }

    public string Decrypt(string ciphertext)
    {
        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new CryptographicException("Unknown field encryption version.");
        }

        var input = Convert.FromBase64String(ciphertext[Prefix.Length..]);
        var plain = new byte[input.Length - NonceSize - TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(input.AsSpan(0, NonceSize), input.AsSpan(NonceSize + TagSize), input.AsSpan(NonceSize, TagSize), plain);
        return Encoding.UTF8.GetString(plain);
    }
}
