using Careers.Application.Catalogue;

namespace Careers.Infrastructure.Catalogue;

/// <summary>
/// Launchpad sectors (launchpad.handlers.ts) and My Matches cards and industries (matches.handlers.ts), per market.
/// Sector and industry fit values are the design's placeholders until Assessment publishes per-sector fit.
/// </summary>
internal sealed class SeedCareerCatalogue : ICareerCatalogue
{
    private static readonly IReadOnlyList<SectorSeed> SectorList =
    [
        new("bpo", "BPO", "🎧", 24, 3, 94),
        new("insurance", "Insurance", "🛡️", 12, 1, 88),
        new("it", "IT", "💻", 31, 4, 84),
        new("hospitality", "Hospitality", "🏨", 9, 0, 81),
        new("retail", "Retail", "🛍️", 18, 2, 79),
        new("banking", "Banking", "🏦", 15, 1, 76),
        new("healthcare", "Healthcare", "🏥", 7, 0, 71),
        new("telecom", "Telecom", "📡", 8, 1, 68),
        new("marketing", "Marketing", "📣", 10, 0, 64),
        new("manufacturing", "Manufacturing", "🏭", 13, 2, 58),
        new("engineering", "Engineering", "⚙️", 11, 0, 52),
        new("pharma", "Pharmaceutical", "💊", 6, 0, 47),
        new("fashion", "Fashion Retail", "👗", 5, 1, 44),
        new("ngo", "NGO", "🌱", 4, 0, 40),
    ];

    private static readonly IReadOnlyList<MatchSeed> India =
    [
        new("m1", "Customer Support Executive — Voice", "Swiggy", "job", "Andheri, Mumbai", "Mumbai", "Maharashtra", "₹3.2 – 4 LPA", "bpo", false, ["Customer service", "Communication", "Hindi", "English"]),
        new("m2", "Back Office Executive", "HDFC Bank", "job", "Powai, Mumbai", "Mumbai", "Maharashtra", "₹2.8 – 3.6 LPA", "finance", false, ["Data capturing", "MS Excel"]),
        new("m3", "Retail Sales Associate", "Reliance Retail", "job", "Malad, Mumbai", "Mumbai", "Maharashtra", "₹2.4 – 3 LPA", "retail", false, ["Customer service", "Weekend availability"]),
        new("m4", "Hotel Front Office Trainee (NAPS)", "Taj Hotels", "apprenticeship", "Colaba, Mumbai", "Mumbai", "Maharashtra", "₹15,000 / month stipend", "hospitality", false, ["Hindi", "English", "Front office basics"]),
        new("m5", "Chat Support Associate", "Teleperformance", "job", "Remote", "Remote", string.Empty, "₹2.6 LPA", "bpo", true, ["Customer service", "Typing"]),
    ];

    private static readonly IReadOnlyList<MatchSeed> SouthAfrica =
    [
        new("m1", "Contact Centre Agent — Learnership", "Ubuntu CX (Pty) Ltd", "learnership", "Randburg", "Johannesburg", "Gauteng", "R 6,500 / month stipend · 12 months", "bpo", false, ["Customer service", "Active listening", "isiZulu", "English", "Call centre systems"]),
        new("m2", "Back Office Assistant", "Sizwe Financial Services", "job", "Sandton", "Johannesburg", "Gauteng", "R 8,000 – 9,500 / month", "finance", false, ["Data capturing", "MS Excel", "Matric (NSC)"]),
        new("m3", "Retail Sales Assistant", "Kasi Retail Group", "job", "Soweto", "Johannesburg", "Gauteng", "R 5,800 – 6,800 / month", "retail", false, ["Customer service", "Conflict resolution", "Weekend availability"]),
        new("m4", "Front Desk Trainee (Hospitality)", "Jozi Hotels", "learnership", "Rosebank", "Johannesburg", "Gauteng", "R 6,000 / month · learnership", "hospitality", false, ["Active listening", "isiZulu", "English", "Hospitality NQF 2"]),
        new("m5", "Chat Support Agent", "Ubuntu CX (Pty) Ltd", "job", "Remote", "Remote", string.Empty, "R 7,200 / month", "bpo", true, ["Customer service", "Typing"]),
        new("m6", "Stock Controller Assistant", "Mzansi Logistics", "job", "Midrand", "Johannesburg", "Gauteng", "R 6,800 / month", "logistics", false, ["Data capturing", "Warehousing basics"]),
    ];

    private static readonly IReadOnlyList<IndustrySeed> IndustryList =
    [
        new("bpo", "Contact centre & BPO", 92, "Social + Enterprising interests · strong communication & patience", "Contact centre agent, Chat support", "Your contact centre certificate fits", true, 38),
        new("retail", "Retail & e-commerce", 84, "Enjoys helping people, works well under pressure", "Sales assistant, Cashier, Store ops", "Short course: Retail basics (8 weeks)", false, 21),
        new("finance", "Financial services — operations", 78, "Detail-focused, good with numbers", "Back office, Data capturer, KYC", "Learning path: Banking basics (4 h)", false, 12),
        new("hospitality", "Hospitality & tourism", 71, "Social interests, multilingual", "Front desk, Guest services", "Learnership: Hospitality level 2", false, 9),
        new("logistics", "Logistics & supply chain admin", 64, "Organised, reliable", "Stock controller, Dispatch clerk", "Short course: Warehousing basics", false, 6),
    ];

    public IReadOnlyList<SectorSeed> Sectors => SectorList;

    public IReadOnlyList<IndustrySeed> Industries => IndustryList;

    public IReadOnlyList<MatchSeed> MatchesFor(string market) => market == "ZA" ? SouthAfrica : India;
}
