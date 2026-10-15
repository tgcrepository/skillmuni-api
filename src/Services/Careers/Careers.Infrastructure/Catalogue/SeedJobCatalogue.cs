using System.Globalization;
using Careers.Application.Catalogue;

namespace Careers.Infrastructure.Catalogue;

/// <summary>
/// The My Jobs catalogue from the frontend mock (jobs.data.ts): the seven jobs drawn in Figma plus generated ones up to
/// 128, built with the mock's deterministic generator (same seed, same order of draws) so ids, counts and facets
/// match the mock exactly. Match info is not seeded; it is computed per learner.
/// </summary>
internal sealed class SeedJobCatalogue : IJobCatalogue
{
    private const int Total = 128;

    private const string AboutCustomerService =
        "Be the first voice customers hear. You’ll help customers of a leading telecom brand over calls and chat, solve their questions and make sure they leave happy. Full training is provided in your first 4 weeks.";

    private static readonly KeyValuePair<string, IReadOnlyList<string>>[] SectorList =
    [
        new("BPO / Contact centre", ["Customer service", "Technical support", "Collections", "Chat support"]),
        new("Insurance", ["Customer service", "Claims processing", "Sales"]),
        new("IT & Tech support", ["Technical support", "Desktop support", "QA tester"]),
        new("Retail", ["Store associate", "Cashier", "Visual merchandiser"]),
        new("Hospitality", ["Front desk", "Guest relations", "F&B service"]),
        new("Banking & Finance", ["Relationship officer", "Back office", "Sales"]),
        new("E-commerce & Logistics", ["Delivery associate", "Warehouse associate", "Customer service"]),
        new("Education", ["Teaching assistant", "Counsellor", "Admissions"]),
        new("Healthcare", ["Patient care", "Front desk", "Pharmacy assistant"]),
        new("Telecom", ["Sales", "Customer service", "Field technician"]),
        new("Manufacturing", ["Machine operator", "Quality inspector", "Store keeper"]),
        new("Media & Marketing", ["Content writer", "Social media assistant", "Graphic designer"]),
        new("Real estate", ["Sales", "Site coordinator"]),
        new("Data & Admin", ["Data entry", "Office assistant", "Back office"]),
    ];

    private static readonly KeyValuePair<string, IReadOnlyList<string>>[] StateList =
    [
        new("Maharashtra", ["Pune", "Mumbai", "Nagpur", "Nashik"]),
        new("Karnataka", ["Bengaluru", "Mysuru"]),
        new("Telangana", ["Hyderabad"]),
        new("Tamil Nadu", ["Chennai"]),
        new("Delhi", ["New Delhi"]),
        new("Goa", ["Goa"]),
    ];

    private static readonly (string Company, string Initials, string Sector)[] Companies =
    [
        ("Teleperformance", "TP", "BPO / Contact centre"),
        ("HDFC Life", "HL", "Insurance"),
        ("Concentrix", "CX", "BPO / Contact centre"),
        ("Reliance Trends", "RT", "Retail"),
        ("Tata AIG", "TA", "Insurance"),
        ("Taj Hotels", "TH", "Hospitality"),
        ("ICICI Lombard", "IL", "Insurance"),
        ("Infosys BPM", "IB", "IT & Tech support"),
        ("Amazon", "AZ", "E-commerce & Logistics"),
        ("Axis Bank", "AX", "Banking & Finance"),
        ("Apollo Hospitals", "AH", "Healthcare"),
        ("Airtel", "AT", "Telecom"),
        ("Tata Motors", "TM", "Manufacturing"),
        ("upGrad", "UG", "Education"),
        ("Zomato", "ZO", "E-commerce & Logistics"),
        ("Wipro", "WI", "IT & Tech support"),
        ("Lodha Group", "LG", "Real estate"),
        ("Ogilvy", "OG", "Media & Marketing"),
        ("Quess Corp", "QC", "Data & Admin"),
    ];

    private static readonly Dictionary<string, string> Titles = new(StringComparer.Ordinal)
    {
        ["Customer service"] = "Customer Service Associate",
        ["Technical support"] = "Technical Support Associate",
        ["Collections"] = "Collections Executive",
        ["Chat support"] = "Chat Support Executive",
        ["Claims processing"] = "Claims Processing Associate",
        ["Sales"] = "Sales Executive",
        ["Desktop support"] = "Desktop Support Engineer",
        ["QA tester"] = "Junior QA Tester",
        ["Store associate"] = "Retail Store Associate",
        ["Cashier"] = "Cashier",
        ["Visual merchandiser"] = "Visual Merchandiser",
        ["Front desk"] = "Front Desk Associate",
        ["Guest relations"] = "Guest Relations Executive",
        ["F&B service"] = "F&B Service Associate",
        ["Relationship officer"] = "Relationship Officer",
        ["Back office"] = "Back Office Executive",
        ["Delivery associate"] = "Delivery Associate",
        ["Warehouse associate"] = "Warehouse Associate",
        ["Teaching assistant"] = "Teaching Assistant",
        ["Counsellor"] = "Career Counsellor",
        ["Admissions"] = "Admissions Executive",
        ["Patient care"] = "Patient Care Assistant",
        ["Pharmacy assistant"] = "Pharmacy Assistant",
        ["Field technician"] = "Field Technician",
        ["Machine operator"] = "Machine Operator",
        ["Quality inspector"] = "Quality Inspector",
        ["Store keeper"] = "Store Keeper",
        ["Content writer"] = "Junior Content Writer",
        ["Social media assistant"] = "Social Media Assistant",
        ["Graphic designer"] = "Junior Graphic Designer",
        ["Site coordinator"] = "Site Coordinator",
        ["Data entry"] = "Data Entry Operator",
        ["Office assistant"] = "Office Assistant",
    };

    private static readonly string[] Modes = ["onsite", "onsite", "onsite", "hybrid", "remote"];
    private static readonly string[] Types = ["full-time", "full-time", "full-time", "learnership", "part-time", "contract"];
    private static readonly string[] PeopleRoles = ["Customer service", "Sales", "Chat support", "Store associate"];

    /// <summary>The mock asks every job the same two questions (jobs.handlers.ts apply info).</summary>
    private static readonly IReadOnlyList<ApplyQuestion> DefaultQuestions =
    [
        new("q1", "Are you comfortable with rotational shifts, including nights?", "yes-no"),
        new("q2", "When can you join?", "text"),
    ];

    private readonly Dictionary<string, CatalogueJob> _byId;

    public SeedJobCatalogue()
    {
        Jobs = Build();
        _byId = Jobs.ToDictionary(j => j.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<CatalogueJob> Jobs { get; }

    public IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> Sectors => SectorList;

    public IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> States => StateList;

    public CatalogueJob? Find(string id) => _byId.GetValueOrDefault(id);

    public IReadOnlyList<ApplyQuestion> QuestionsFor(string jobId) => DefaultQuestions;

    internal static string PayLabel(string type, double min, double max)
    {
        if (type == "learnership")
        {
            return $"₹{(min * 1000).ToString("#,##0", CultureInfo.InvariantCulture)} / month stipend";
        }

        return min == max
            ? $"₹{min.ToString("0.0", CultureInfo.InvariantCulture)} LPA"
            : $"₹{min.ToString("0.0", CultureInfo.InvariantCulture)}–{max.ToString("0.0", CultureInfo.InvariantCulture)} LPA";
    }

    private static List<CatalogueJob> Build()
    {
        var r = new MockRandom(42);
        var fixedSeeds = new Seed[]
        {
            new("Customer Service Associate", "Teleperformance", "TP", "Pune", "Maharashtra", "BPO / Contact centre", "Customer service", "onsite", "full-time", null, 2.4, 3.0, 2, null),
            new("Inbound Sales Executive", "HDFC Life", "HL", "Mumbai", "Maharashtra", "Insurance", "Sales", "hybrid", "full-time", null, 3.0, 4.0, 5, 3),
            new("Technical Support Trainee", "Concentrix", "CX", "Bengaluru", "Karnataka", "BPO / Contact centre", "Technical support", "onsite", "learnership", null, 15, 15, 1, null),
            new("Retail Store Associate", "Reliance Trends", "RT", "Pune", "Maharashtra", "Retail", "Store associate", "onsite", "part-time", null, 1.8, 1.8, 7, null),
            new("Data Entry Operator", "Tata AIG", "TA", "Remote", string.Empty, "Data & Admin", "Data entry", "remote", "contract", 6, 2.2, 2.2, 3, null),
            new("Front Desk Associate", "Taj Hotels", "TH", "Goa", "Goa", "Hospitality", "Front desk", "onsite", "full-time", null, 2.6, 2.6, 14, null),
            new("Claims Processing Associate", "ICICI Lombard", "IL", "Pune", "Maharashtra", "Insurance", "Claims processing", "onsite", "full-time", null, 2.8, 2.8, 4, null),
        };
        var jobs = fixedSeeds.Select((s, i) => Make($"job-{i + 1}", s, r)).ToList();

        // Draws happen in exactly the mock's order (jobs.data.ts build()), so the catalogue is identical.
        for (var i = jobs.Count; i < Total; i++)
        {
            var (company, initials, home) = Companies[Pick(r, Companies.Length)];
            var sector = r.Next() < 0.7 ? home : SectorList[Pick(r, 14)].Key;
            var roles = SectorList.First(s => s.Key == sector).Value;
            var role = roles[Pick(r, roles.Count)];
            var state = r.Next() < 0.6 ? "Maharashtra" : StateList[Pick(r, StateList.Length)].Key;
            var cities = StateList.First(s => s.Key == state).Value;
            var city = state == "Maharashtra" && r.Next() < 0.55 ? "Pune" : cities[Pick(r, cities.Count)];
            var workMode = Modes[Pick(r, Modes.Length)];
            var jobType = Types[Pick(r, Types.Length)];
            var @base = 1.6 + (JsRound(r.Next() * 30) / 10);
            double min, max;
            if (jobType == "learnership")
            {
                min = 10 + Pick(r, 8);
                max = min;
            }
            else
            {
                min = @base;
                max = @base + (JsRound(r.Next() * 10) / 10);
            }

            var postedDays = 1 + Pick(r, 40);
            int? closingInDays = r.Next() < 0.08 ? 2 + Pick(r, 4) : null;

            // The mock scores some people-facing roles here; the draws must still happen to keep the sequence.
            if (jobs.Count < 60 && PeopleRoles.Contains(role))
            {
                _ = Pick(r, 18);
                _ = r.Next();
            }

            jobs.Add(Make(
                $"job-{i + 1}",
                new Seed(Titles.GetValueOrDefault(role, role), company, initials, city, state, sector, role, workMode, jobType, 6, min, max, postedDays, closingInDays),
                r));
        }

        return jobs;
    }

    private static CatalogueJob Make(string id, Seed s, MockRandom r)
    {
        var remote = s.WorkMode == "remote";
        var bpo = s.Sector == "BPO / Contact centre";
        var customerService = s.Role == "Customer service";
        var typeLabel = s.JobType switch
        {
            "contract" => $"Contract · {s.Months ?? 6} months",
            "learnership" => "Learnership",
            "part-time" => "Part-time",
            _ => "Full-time",
        };
        var applicants = 12 + Pick(r, 80);
        var openings = 2 + Pick(r, 14);
        var article = "AEIOU".Contains(s.Title[0], StringComparison.Ordinal) ? "an" : "a";
        return new CatalogueJob(
            id,
            s.Title,
            s.Company,
            s.Initials,
            remote ? "Remote" : s.City,
            remote ? string.Empty : s.State,
            remote ? "Remote · India" : s.City == s.State ? s.City : $"{s.City}, {s.State}",
            s.Sector,
            s.Role,
            s.WorkMode,
            s.JobType,
            typeLabel,
            PayLabel(s.JobType, s.Min, s.Max),
            s.JobType == "learnership" ? s.Min * 12 / 100 : s.Min,
            s.PostedDays,
            applicants,
            "15 Oct",
            s.ClosingInDays,
            false,
            "0–1 years (freshers welcome)",
            openings,
            bpo ? "Rotational · 5 days/week" : "Day · 6 days/week",
            customerService
                ? AboutCustomerService
                : $"Join {s.Company} as {article} {s.Title}. You’ll learn on the job with a buddy for your first month and grow into a full role.",
            customerService
                ? ["Handle 40–60 customer calls and chats a day", "Log every interaction in the CRM", "Escalate complex issues to the right team"]
                : ["Learn the team’s tools in your first two weeks", "Own a daily set of tasks and targets", "Share updates with your team lead"],
            bpo
                ? ["12th pass or graduate", "Good spoken English and Hindi", "Comfortable with rotational shifts", "Basic computer skills (MS Office)"]
                : ["12th pass or graduate", "Good spoken English and Hindi", "Basic computer skills (MS Office)"],
            customerService
                ? ["Communication", "Customer service", "English", "Hindi", "CRM", "MS Office"]
                : ["Communication", "Teamwork", "MS Office"],
            $"{s.Company} is a leading employer in {s.Sector.ToLowerInvariant()} with teams across India. Benefits: health insurance, performance bonus{(bpo ? ", cab facility for night shifts" : string.Empty)}.");
    }

    private static int Pick(MockRandom r, int count) => (int)Math.Floor(r.Next() * count);

    /// <summary>JavaScript's Math.round (halves round up).</summary>
    private static double JsRound(double value) => Math.Floor(value + 0.5);

    private sealed record Seed(
        string Title,
        string Company,
        string Initials,
        string City,
        string State,
        string Sector,
        string Role,
        string WorkMode,
        string JobType,
        int? Months,
        double Min,
        double Max,
        int PostedDays,
        int? ClosingInDays);

    /// <summary>The mock's linear congruential generator (jobs.data.ts <c>rng</c>), bit-for-bit.</summary>
    private sealed class MockRandom(long seed)
    {
        private const long Modulus = 4294967296;
        private long _state = seed;

        public double Next()
        {
            _state = ((_state * 1664525) + 1013904223) % Modulus;
            return _state / (double)Modulus;
        }
    }
}
