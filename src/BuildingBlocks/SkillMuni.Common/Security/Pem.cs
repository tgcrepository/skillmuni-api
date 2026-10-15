using System.Text;

namespace SkillMuni.Common.Security;

public static class Pem
{
    /// <summary>
    /// Accepts a PEM block as-is, with literal <c>\n</c> escapes, or base64-encoded (convenient for env vars and
    /// Key Vault secrets).
    /// </summary>
    public static string Decode(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("-----BEGIN", StringComparison.Ordinal))
        {
            return trimmed.Replace("\\n", "\n", StringComparison.Ordinal);
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(trimmed));
    }
}
