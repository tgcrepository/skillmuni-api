using System.Globalization;
using Learner.Domain;

namespace Learner.Application.Services;

/// <summary>How private details are masked and displayed (Profile v2 · Player ID). Masking happens on the server.</summary>
internal static class ProfileFormat
{
    public const string NotAdded = "Not added";

    private static readonly Dictionary<string, string> DocumentLabels = new(StringComparer.Ordinal)
    {
        ["aadhaar"] = "Aadhaar",
        ["pan"] = "PAN",
        ["driving-licence"] = "Driving licence",
    };

    public static string MaskMobile(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digits.Length >= 4 ? $"•••••• {digits[^4..]}" : NotAdded;
    }

    public static string MaskEmail(string? email)
    {
        var at = email?.IndexOf('@', StringComparison.Ordinal) ?? -1;
        return email is null || at < 1 ? NotAdded : $"{email[0]}••••{email[at..]}";
    }

    /// <summary>"+919876540697" → "+91 98765 40697"; other numbers are shown as stored.</summary>
    public static string DisplayPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone))
        {
            return string.Empty;
        }

        return phone.StartsWith("+91", StringComparison.Ordinal) && phone.Length == 13
            ? $"+91 {phone[3..8]} {phone[8..]}"
            : phone;
    }

    public static string MaskDob(DateOnly? dob) =>
        dob is { } d ? $"•• ••• {d.Year.ToString(CultureInfo.InvariantCulture)}" : NotAdded;

    public static string DisplayDob(DateOnly? dob) =>
        dob?.ToString("d MMM yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    public static string IsoDate(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    public static string DocumentLabel(string document) => DocumentLabels.GetValueOrDefault(document, "ID");

    public static string GenderKey(Gender? gender) => gender switch
    {
        Gender.Female => "female",
        Gender.Male => "male",
        Gender.NonBinary => "non-binary",
        _ => "rather-not",
    };

    public static Gender ParseGender(string key) => key switch
    {
        "female" => Gender.Female,
        "male" => Gender.Male,
        "non-binary" => Gender.NonBinary,
        _ => Gender.RatherNot,
    };

    public static string GenderLabel(Gender? gender) => gender switch
    {
        Gender.Female => "Female",
        Gender.Male => "Male",
        Gender.NonBinary => "Non-binary",
        _ => "Rather not say",
    };

    public static string Initials(LearnerProfile learner)
    {
        var first = learner.FirstName.Length > 0 ? learner.FirstName[..1] : string.Empty;
        var last = learner.LastName.Length > 0 ? learner.LastName[..1] : string.Empty;
        return (first + last).ToUpperInvariant();
    }

    /// <summary>A short, stable, non-sequential id for the Player ID card.</summary>
    public static string PlayerId(Guid learnerId) => $"SM-{learnerId.ToString("N")[..6].ToUpperInvariant()}";
}
