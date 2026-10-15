using System.Text;
using System.Text.RegularExpressions;

namespace Identity.Domain.Common;

/// <summary>
/// Per-market rules (approved decision 3: India first, South Africa through configuration). Mirrors the
/// frontend's <c>MARKETS</c> table so a number the app accepts is a number the server accepts.
/// </summary>
public sealed class Market
{
    public static readonly Market India = new(
        code: "IN",
        dialCode: "91",
        nationalDigits: 10,
        mobilePattern: new Regex(@"^[6-9]\d{9}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
        groups: [5, 5],
        privacyLaw: "DPDP");

    public static readonly Market SouthAfrica = new(
        code: "ZA",
        dialCode: "27",
        nationalDigits: 9,
        mobilePattern: new Regex(@"^[6-8]\d{8}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
        groups: [2, 3, 4],
        privacyLaw: "POPIA");

    private readonly Regex _mobilePattern;
    private readonly int[] _groups;

    private Market(string code, string dialCode, int nationalDigits, Regex mobilePattern, int[] groups, string privacyLaw)
    {
        Code = code;
        DialCode = dialCode;
        NationalDigits = nationalDigits;
        _mobilePattern = mobilePattern;
        _groups = groups;
        PrivacyLaw = privacyLaw;
    }

    public string Code { get; }

    /// <summary>Country calling code without the plus, e.g. <c>91</c>.</summary>
    public string DialCode { get; }

    public int NationalDigits { get; }

    /// <summary>The law consent records cite: DPDP Act 2023 (IN) or POPIA (ZA).</summary>
    public string PrivacyLaw { get; }

    public static Market For(string? code) =>
        string.Equals(code, SouthAfrica.Code, StringComparison.OrdinalIgnoreCase) ? SouthAfrica : India;

    /// <summary>
    /// "+91 98765-43210", "098765 43210" or "9876543210" → "+919876543210" (E.164), or null when it isn't a
    /// valid mobile number for this market.
    /// </summary>
    public string? NormalizeMobile(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        var hasPlus = raw.TrimStart().StartsWith('+');
        if ((hasPlus || digits.Length == NationalDigits + DialCode.Length) && digits.StartsWith(DialCode, StringComparison.Ordinal))
        {
            digits = digits[DialCode.Length..];
        }
        else if (hasPlus)
        {
            return null;
        }

        if (digits.Length == NationalDigits + 1 && digits[0] == '0')
        {
            digits = digits[1..];
        }

        return _mobilePattern.IsMatch(digits) ? $"+{DialCode}{digits}" : null;
    }

    /// <summary>"+919876543210" → "+91 98765 43210" (how the app shows a number the learner typed).</summary>
    public string FormatMobile(string e164)
    {
        var national = National(e164);
        var builder = new StringBuilder($"+{DialCode}");
        var at = 0;
        foreach (var size in _groups)
        {
            if (at >= national.Length)
            {
                break;
            }

            var take = Math.Min(size, national.Length - at);
            builder.Append(' ').Append(national, at, take);
            at += take;
        }

        return builder.ToString();
    }

    /// <summary>"+919876544521" → "+91 98•••• 4521" (claim screens show only the ends).</summary>
    public string MaskMobile(string e164)
    {
        var national = National(e164);
        if (national.Length < 7)
        {
            return $"+{DialCode} ••••";
        }

        return $"+{DialCode} {national[..2]}{new string('•', national.Length - 6)} {national[^4..]}";
    }

    private string National(string e164)
    {
        var digits = e164.TrimStart('+');
        return digits.StartsWith(DialCode, StringComparison.Ordinal) ? digits[DialCode.Length..] : digits;
    }
}
