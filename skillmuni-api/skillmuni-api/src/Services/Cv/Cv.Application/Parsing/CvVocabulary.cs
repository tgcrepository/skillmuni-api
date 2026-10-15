namespace Cv.Application.Parsing;

public sealed record SkillTerm(string Id, string Name, string Category, params string[] Synonyms);

/// <summary>
/// Words the parser recognises. The skill list is a starter set of entry-level skills with the deck ids the quest
/// screens use; flagged to replace with the frontend's full ESCO deck (956 skills, <c>data/skills.ts</c>).
/// </summary>
public static class CvVocabulary
{
    public static readonly IReadOnlyList<SkillTerm> Skills =
    [
        new("customer-service", "Customer service", "people", "customer support", "customer care", "client service"),
        new("active-listening", "Active listening", "people", "listening skills"),
        new("communication", "Communication", "people", "communication skills"),
        new("verbal-communication", "Verbal communication", "people", "spoken communication"),
        new("written-communication", "Written communication", "people", "business writing"),
        new("conflict-resolution", "Conflict resolution", "people", "complaint handling", "handling complaints"),
        new("teamwork", "Teamwork", "people", "team player", "team work", "collaboration"),
        new("problem-solving", "Problem solving", "people", "problem-solving", "troubleshooting"),
        new("time-management", "Time management", "people"),
        new("leadership", "Leadership", "people", "team lead", "team leadership"),
        new("negotiation", "Negotiation", "people"),
        new("call-centre-systems", "Call centre systems", "tech", "call center systems", "dialler", "dialer", "avaya", "genesys"),
        new("excel", "MS Excel", "data", "microsoft excel", "ms-excel", "excel", "spreadsheets"),
        new("ms-office", "MS Office", "business", "microsoft office", "ms word", "microsoft word", "powerpoint"),
        new("data-capturing", "Data capturing", "data", "data capture"),
        new("data-entry", "Data entry", "data", "data-entry"),
        new("typing", "Typing", "data", "touch typing", "wpm"),
        new("scheduling", "Scheduling", "business", "appointment setting", "calendar management"),
        new("sales", "Sales", "business", "selling", "retail sales"),
        new("cold-calling", "Cold calling", "business", "telesales", "outbound calls"),
        new("cash-handling", "Cash handling", "business", "cashier", "till operation", "pos"),
        new("bookkeeping", "Bookkeeping", "business", "book keeping", "accounts"),
        new("tally", "Tally", "business", "tally erp", "tally prime"),
        new("zoho-crm", "Zoho CRM", "business", "zoho"),
        new("salesforce", "Salesforce", "business"),
        new("crm", "CRM software", "business", "crm"),
        new("inventory", "Inventory management", "business", "stock control", "stock taking"),
        new("marketing", "Marketing", "creative", "digital marketing"),
        new("social-media", "Social media", "creative", "instagram", "facebook marketing", "social media marketing"),
        new("graphic-design", "Graphic design", "creative", "canva", "photoshop", "illustrator"),
        new("content-writing", "Content writing", "creative", "copywriting", "blogging"),
        new("video-editing", "Video editing", "creative", "premiere pro"),
        new("programming", "Programming", "tech", "coding"),
        new("java", "Java", "tech"),
        new("python", "Python", "tech"),
        new("javascript", "JavaScript", "tech", "js"),
        new("html-css", "HTML & CSS", "tech", "html", "css"),
        new("sql", "SQL", "data", "mysql", "database"),
        new("computer-literacy", "Computer literacy", "tech", "basic computer", "computer skills"),
        new("it-support", "IT support", "tech", "technical support", "help desk", "helpdesk"),
        new("data-analysis", "Data analysis", "data", "data analytics", "analysis"),
        new("first-aid", "First aid", "trades"),
        new("driving", "Driving", "trades", "driver's licence", "driving licence", "drivers license"),
        new("electrical", "Electrical work", "trades", "electrician", "wiring"),
        new("plumbing", "Plumbing", "trades", "plumber"),
        new("welding", "Welding", "trades", "welder"),
        new("machine-operation", "Machine operation", "trades", "machine operator"),
        new("housekeeping", "Housekeeping", "trades", "cleaning"),
        new("cooking", "Cooking", "trades", "food preparation", "chef"),
        new("warehousing", "Warehousing", "trades", "warehouse", "forklift"),
    ];

    public static readonly IReadOnlyDictionary<string, string[]> Languages = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["IN"] =
        [
            "English", "Hindi", "Marathi", "Bengali", "Telugu", "Tamil", "Gujarati", "Urdu", "Kannada", "Odia",
            "Malayalam", "Punjabi", "Assamese", "Konkani", "Maithili", "Nepali", "Sindhi", "Bhojpuri", "Rajasthani", "Tulu",
        ],
        ["ZA"] =
        [
            "English", "isiZulu", "isiXhosa", "Afrikaans", "Sepedi", "Setswana", "Sesotho", "Xitsonga", "siSwati",
            "Tshivenda", "isiNdebele",
        ],
    };

    /// <summary>Common spellings on CVs → the chip label the Quest 4 screen uses.</summary>
    public static readonly IReadOnlyDictionary<string, string> LanguageAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["zulu"] = "isiZulu",
        ["xhosa"] = "isiXhosa",
        ["sotho"] = "Sesotho",
        ["southern sotho"] = "Sesotho",
        ["tswana"] = "Setswana",
        ["pedi"] = "Sepedi",
        ["northern sotho"] = "Sepedi",
        ["tsonga"] = "Xitsonga",
        ["swati"] = "siSwati",
        ["venda"] = "Tshivenda",
        ["ndebele"] = "isiNdebele",
        ["oriya"] = "Odia",
    };

    /// <summary>City → state / province, for the Quest 2 area row. A starter list; the geo lookup has the full set.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Cities =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["IN"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Mumbai"] = "Maharashtra", ["Pune"] = "Maharashtra", ["Nagpur"] = "Maharashtra", ["Nashik"] = "Maharashtra",
                ["Delhi"] = "Delhi", ["New Delhi"] = "Delhi", ["Noida"] = "Uttar Pradesh", ["Lucknow"] = "Uttar Pradesh",
                ["Gurugram"] = "Haryana", ["Gurgaon"] = "Haryana", ["Bengaluru"] = "Karnataka", ["Bangalore"] = "Karnataka",
                ["Mysuru"] = "Karnataka", ["Chennai"] = "Tamil Nadu", ["Coimbatore"] = "Tamil Nadu", ["Hyderabad"] = "Telangana",
                ["Kolkata"] = "West Bengal", ["Ahmedabad"] = "Gujarat", ["Surat"] = "Gujarat", ["Jaipur"] = "Rajasthan",
                ["Indore"] = "Madhya Pradesh", ["Bhopal"] = "Madhya Pradesh", ["Patna"] = "Bihar", ["Kochi"] = "Kerala",
                ["Thiruvananthapuram"] = "Kerala", ["Bhubaneswar"] = "Odisha", ["Chandigarh"] = "Chandigarh", ["Guwahati"] = "Assam",
            },
            ["ZA"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Johannesburg"] = "Gauteng", ["Randburg"] = "Gauteng", ["Soweto"] = "Gauteng", ["Sandton"] = "Gauteng",
                ["Pretoria"] = "Gauteng", ["Tshwane"] = "Gauteng", ["Midrand"] = "Gauteng", ["Cape Town"] = "Western Cape",
                ["Stellenbosch"] = "Western Cape", ["Durban"] = "KwaZulu-Natal", ["Pietermaritzburg"] = "KwaZulu-Natal",
                ["Gqeberha"] = "Eastern Cape", ["Port Elizabeth"] = "Eastern Cape", ["East London"] = "Eastern Cape",
                ["Bloemfontein"] = "Free State", ["Polokwane"] = "Limpopo", ["Mbombela"] = "Mpumalanga", ["Kimberley"] = "Northern Cape",
                ["Rustenburg"] = "North West", ["Mahikeng"] = "North West",
            },
        };
}
