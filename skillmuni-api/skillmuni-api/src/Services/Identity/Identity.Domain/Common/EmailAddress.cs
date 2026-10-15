using System.Text.RegularExpressions;

namespace Identity.Domain.Common;

public static class EmailAddress
{
    public const int MaxLength = 254;

    private static readonly Regex Shape = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>Trimmed and lower-cased, or null when it doesn't look like an email address.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var email = raw.Trim().ToLowerInvariant();
        return email.Length <= MaxLength && Shape.IsMatch(email) ? email : null;
    }

    /// <summary>"yash.p@gmail.com" → "ya•••@gmail.com" (what the code screen shows).</summary>
    public static string Mask(string email)
    {
        var at = email.LastIndexOf('@');
        if (at < 1)
        {
            return "•••";
        }

        var name = email[..at];
        return $"{name[..Math.Min(2, name.Length)]}•••{email[at..]}";
    }
}
