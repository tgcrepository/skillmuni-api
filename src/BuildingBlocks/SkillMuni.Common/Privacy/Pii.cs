namespace SkillMuni.Common.Privacy;

/// <summary>Masks personal data before it goes into a log line (CLAUDE.md §6: no PII in logs).</summary>
public static class Pii
{
    /// <summary><c>yash.p@gmail.com</c> → <c>y***@gmail.com</c>.</summary>
    public static string Email(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return string.Empty;
        }

        var at = email.LastIndexOf('@');
        return at < 1 ? "***" : $"{email[0]}***{email[at..]}";
    }

    /// <summary><c>+919876543210</c> → <c>***3210</c>.</summary>
    public static string Phone(string? phone)
    {
        if (string.IsNullOrEmpty(phone))
        {
            return string.Empty;
        }

        return phone.Length <= 4 ? "***" : $"***{phone[^4..]}";
    }

    /// <summary>Masks whichever kind of contact this is.</summary>
    public static string Contact(string? value) =>
        value is not null && value.Contains('@', StringComparison.Ordinal) ? Email(value) : Phone(value);
}
