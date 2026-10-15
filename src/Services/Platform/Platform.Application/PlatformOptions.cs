namespace Platform.Application;

/// <summary>Bound from the <c>Platform</c> config section; every value has a working default.</summary>
public sealed class PlatformOptions
{
    public const string Section = "Platform";

    /// <summary>Market code → config (MISS-12). Empty = <see cref="MarketSettings.Defaults"/>.</summary>
    public Dictionary<string, MarketSettings> Markets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Shown when a tenant has no theme row yet.</summary>
    public string DefaultThemeName { get; set; } = "Skillmuni";

    /// <summary>Tenant themes and postal lookups are cached this long (Redis).</summary>
    public TimeSpan CacheFor { get; set; } = TimeSpan.FromMinutes(10);

    public IReadOnlyDictionary<string, MarketSettings> AllMarkets() =>
        Markets.Count > 0 ? Markets : MarketSettings.Defaults;
}

/// <summary>
/// One market's rules, as the app's <c>MarketConfig</c> (src/app/core/market/market.ts) holds them today. Patterns are
/// JavaScript regular expression sources; <c>check</c> names an extra client-side check (e.g. <c>luhn</c>).
/// </summary>
public sealed class MarketSettings
{
    public static readonly IReadOnlyDictionary<string, MarketSettings> Defaults = new Dictionary<string, MarketSettings>(StringComparer.OrdinalIgnoreCase)
    {
        ["IN"] = new()
        {
            Code = "IN",
            Name = "India",
            Locale = "en-IN",
            DialCode = "+91",
            TimeZone = "Asia/Kolkata",
            Mobile = new() { Digits = 10, Pattern = @"^[6-9]\d{9}$", Example = "98765 43210" },
            Postal = new() { Label = "PIN code", Pattern = @"^[1-9]\d{5}$", Example = "560001" },
            ConsentLaw = "Digital Personal Data Protection Act, 2023",
            IdTypes =
            [
                new() { Id = "aadhaar", Label = "Aadhaar", Pattern = @"^[2-9]\d{11}$" },
                new() { Id = "pan", Label = "PAN card", Pattern = @"^[A-Z]{5}\d{4}[A-Z]$" },
                new() { Id = "driving-licence", Label = "Driving licence", Pattern = @"^[A-Z]{2}\d{2}\s?\d{11}$" },
            ],
            IdVerification = "digilocker",
            Languages =
            [
                "Hindi", "English", "Bengali", "Marathi", "Telugu", "Tamil", "Gujarati", "Urdu", "Kannada", "Odia", "Malayalam",
                "Punjabi", "Assamese", "Konkani", "Maithili", "Santali", "Kashmiri", "Nepali", "Sindhi", "Dogri", "Manipuri", "Bodo",
                "Sanskrit", "Bhojpuri", "Rajasthani", "Tulu",
            ],
            AsksRace = false,
            IdHasBirthday = [],
        },
        ["ZA"] = new()
        {
            Code = "ZA",
            Name = "South Africa",
            Locale = "en-ZA",
            DialCode = "+27",
            TimeZone = "Africa/Johannesburg",
            Mobile = new() { Digits = 9, Pattern = @"^[6-8]\d{8}$", Example = "71 234 5678" },
            Postal = new() { Label = "Postal code", Pattern = @"^\d{4}$", Example = "2001" },
            ConsentLaw = "Protection of Personal Information Act (POPIA)",
            IdTypes =
            [
                new() { Id = "sa-id", Label = "SA ID number", Pattern = @"^\d{13}$", Check = "luhn" },
                new() { Id = "passport", Label = "Passport", Pattern = "^[A-Z0-9]{6,9}$" },
            ],
            IdVerification = "number",
            Languages =
            [
                "isiZulu", "English", "isiXhosa", "Afrikaans", "Sepedi", "Setswana", "Sesotho", "Xitsonga", "siSwati", "Tshivenda",
                "isiNdebele", "SA Sign Language",
            ],
            AsksRace = true,
            IdHasBirthday = ["sa-id"],
        },
    };

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Locale { get; set; } = string.Empty;

    public string DialCode { get; set; } = string.Empty;

    /// <summary>IANA zone for the learner's day (streaks, daily quests): IN Asia/Kolkata, ZA Africa/Johannesburg.</summary>
    public string TimeZone { get; set; } = string.Empty;

    public MobileRule Mobile { get; set; } = new();

    public PostalRule Postal { get; set; } = new();

    public string ConsentLaw { get; set; } = string.Empty;

    public List<IdTypeRule> IdTypes { get; set; } = [];

    /// <summary><c>digilocker</c> (IN, A-272) or <c>number</c>.</summary>
    public string IdVerification { get; set; } = "number";

    public List<string> Languages { get; set; } = [];

    public bool AsksRace { get; set; }

    public List<string> IdHasBirthday { get; set; } = [];
}

public sealed class MobileRule
{
    public int Digits { get; set; }

    public string Pattern { get; set; } = string.Empty;

    public string Example { get; set; } = string.Empty;
}

public sealed class PostalRule
{
    public string Label { get; set; } = string.Empty;

    public string Pattern { get; set; } = string.Empty;

    public string Example { get; set; } = string.Empty;
}

public sealed class IdTypeRule
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Pattern { get; set; } = string.Empty;

    public string? Check { get; set; }
}
