using Careers.Application.Catalogue;

namespace Careers.Infrastructure.Catalogue;

/// <summary>
/// The Hire Fest catalogue from the frontend mock (fest.handlers.ts): four upcoming fests with their employers and the
/// mock's deterministic slot seats. Seats taken by bookings are subtracted by the use cases. Desk numbers, hall names
/// and total seats are added here (the mock derived or omitted them).
/// </summary>
internal sealed class SeedFestCatalogue : IFestCatalogue
{
    private static readonly string[] Cg = ["10:00", "10:30", "11:00", "11:30", "14:00", "14:30"];
    private static readonly string[] Tp = ["10:30", "11:00", "12:00", "12:30", "15:00", "15:30"];
    private static readonly string[] DefaultTimes = ["10:00", "11:00", "11:30", "12:30", "14:30", "16:00"];

    /// <summary>The Figma frames' fixed seats for Tech &amp; BPO day 1.</summary>
    private static readonly Dictionary<string, Dictionary<string, int>> Day1Seats = new(StringComparer.Ordinal)
    {
        ["cg"] = new(StringComparer.Ordinal) { ["10:00"] = 2, ["10:30"] = 3, ["11:00"] = 0, ["11:30"] = 4, ["14:00"] = 3, ["14:30"] = 6 },
        ["tp"] = new(StringComparer.Ordinal) { ["10:30"] = 2, ["11:00"] = 5, ["12:00"] = 3, ["12:30"] = 2, ["15:00"] = 0, ["15:30"] = 4 },
    };

    private static readonly string[] InPersonBeforeYouGo =
    [
        "Your CV is attached automatically from My Job Profile",
        "Photo ID + this ticket (QR) at the entry desk",
        "Dress: smart casual · arrive 15 min early",
    ];

    private readonly Dictionary<string, CatalogueFest> _byId;

    public SeedFestCatalogue()
    {
        Fests =
        [
            new(
                "tech-bpo-pune",
                "Tech & BPO Hire Fest",
                "in-person",
                "Pune",
                new DateOnly(2026, 12, 1),
                new DateOnly(2026, 12, 3),
                "1 – 3 Dec 2026",
                "10:00 am – 6:00 pm · 30-min interviews",
                "Pune IT Park, Hall B, Hinjewadi",
                "Entry from Gate 2",
                "Hall B",
                "12 km from you",
                "SkillMuni Careers",
                [
                    E("cg", "Cognizant", "CG", "Customer Support", 12, 6, "Day 1–3", 94, 4),
                    E("tp", "Teleperformance", "TP", "Voice Process Exec", 10, 11, "Day 1–2", 90, 9),
                    E("hl", "HDFC Life", "HL", "Inbound Sales Exec", 6, 14, "Day 2–3", 82, 3),
                    E("cx", "Concentrix", "CX", "Tech Support Trainee", 8, 0, "Day 1–3", 76, 5),
                    E("il", "ICICI Lombard", "IL", "Claims Support", 4, 9, "Day 1", 71, 6),
                    E("ta", "Tata AIG", "TA", "Customer Care Exec", 5, 7, "Day 2", 68, 7),
                    E("ax", "Axis Bank", "AX", "Relationship Officer", 6, 12, "Day 3", 63, 8),
                    E("wi", "Wipro", "WI", "Service Desk Analyst", 7, 8, "Day 1–2", 58, 10),
                ],
                8,
                46,
                "Free",
                true,
                100,
                10,
                5,
                "30 Nov, 6 pm",
                "Cancel anytime up to 24 hrs before",
                InPersonBeforeYouGo,
                false),
            new(
                "online-cs",
                "Online Hire Fest: Customer Support",
                "online",
                "Online",
                new DateOnly(2027, 1, 14),
                new DateOnly(2027, 1, 14),
                "Wed, 14 Jan 2027",
                "10:00 am – 6:00 pm IST · 30-min video interviews",
                "Online on SkillMuni Meet",
                "Join link + test call unlock after you book",
                "Online room",
                null,
                "SkillMuni Careers",
                [
                    E("hl", "HDFC Life", "HL", "Inbound Sales Exec", 6, 14, "Day 1", 92, 1),
                    E("il", "ICICI Lombard", "IL", "Claims Support", 4, 9, "Day 1", 88, 2),
                    E("ta", "Tata AIG", "TA", "Customer Care Exec", 5, 20, "Day 1", 81, 3),
                    E("tp", "Teleperformance", "TP", "Chat Support Exec", 15, 18, "Day 1", 79, 4),
                    E("zo", "Zomato", "ZO", "Customer Delight", 10, 16, "Day 1", 74, 5),
                    E("az", "Amazon", "AZ", "Virtual Customer Service", 20, 25, "Day 1", 72, 6),
                    E("at", "Airtel", "AT", "Customer Service", 8, 10, "Day 1", 70, 7),
                    E("ib", "Infosys BPM", "IB", "Process Executive", 12, 14, "Day 1", 66, 8),
                    E("qc", "Quess Corp", "QC", "Back Office", 6, 9, "Day 1", 61, 9),
                    E("ug", "upGrad", "UG", "Learner Support", 4, 6, "Day 1", 58, 10),
                ],
                10,
                32,
                "Free",
                true,
                300,
                120,
                8,
                "13 Jan, 6 pm",
                "Cancel anytime up to 24 hrs before",
                [
                    "Your CV is attached automatically from My Job Profile",
                    "Quiet spot, earphones, 1 Mbps+ internet",
                    "Do the 1-min test call the day before",
                ],
                false),
            new(
                "retail-mumbai",
                "Retail & Hospitality Hire Fest",
                "in-person",
                "Mumbai",
                new DateOnly(2026, 12, 20),
                new DateOnly(2026, 12, 20),
                "Sun, 20 Dec 2026",
                "10:00 am – 5:00 pm · 30-min interviews",
                "NESCO Centre, Goregaon",
                "Hall 3, ground floor",
                "Hall 3",
                "148 km from you",
                "SkillMuni Careers",
                [
                    E("rt", "Reliance Trends", "RT", "Store Associate", 14, 8, "Day 1", 81, 2),
                    E("tj", "Taj Hotels", "TJ", "Front Desk Associate", 6, 5, "Day 1", 77, 3),
                    E("dm", "DMart", "DM", "Customer Associate", 20, 12, "Day 1", 72, 4),
                    E("lf", "Lifestyle", "LF", "Retail Associate", 10, 9, "Day 1", 64, 5),
                ],
                6,
                28,
                "Free",
                true,
                80,
                34,
                3,
                "18 Dec, 6 pm",
                "Cancel anytime up to 24 hrs before",
                InPersonBeforeYouGo,
                false),
            new(
                "fintech-blr",
                "Fintech Careers Day",
                "in-person",
                "Bengaluru",
                new DateOnly(2027, 1, 28),
                new DateOnly(2027, 1, 28),
                "Thu, 28 Jan 2027",
                "10:00 am – 5:00 pm · 30-min interviews",
                "KTPO Convention Centre, Whitefield",
                "Hall A",
                "Hall A",
                "840 km from you",
                "FinSkills Foundation",
                [
                    E("pz", "PhonePe", "PZ", "Merchant Support", 6, 0, "Day 1", 66, 1),
                    E("rz", "Razorpay", "RZ", "Customer Success Associate", 4, 0, "Day 1", 62, 2),
                    E("cr", "CRED", "CR", "Member Support", 3, 0, "Day 1", 55, 3),
                ],
                5,
                18,
                "₹199",
                false,
                60,
                0,
                1,
                "26 Jan, 6 pm",
                "Full refund up to 24 hrs before",
                [
                    "Your CV is attached automatically from My Job Profile",
                    "Photo ID + this ticket (QR) at the entry desk",
                    "Dress: business casual",
                ],
                true),
        ];
        _byId = Fests.ToDictionary(f => f.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<CatalogueFest> Fests { get; }

    public CatalogueFest? Find(string id) => _byId.GetValueOrDefault(id);

    public IReadOnlyList<SlotSeed> Slots(string festId, string employerId, DateOnly date)
    {
        var times = employerId switch
        {
            "cg" => Cg,
            "tp" => Tp,
            _ => DefaultTimes,
        };
        var iso = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var fixedDay = festId == "tech-bpo-pune" && iso == "2026-12-01" && Day1Seats.TryGetValue(employerId, out var day) ? day : null;
        return times.Select(t => new SlotSeed(t, fixedDay is not null ? fixedDay.GetValueOrDefault(t, 1) : Seats(festId, employerId, iso, t))).ToList();
    }

    /// <summary>The mock's deterministic seats per slot (fest.handlers.ts <c>seats</c>): 0 to 6.</summary>
    internal static int Seats(string festId, string employerId, string date, string time)
    {
        var h = 0;
        foreach (var c in $"{festId}{employerId}{date}{time}")
        {
            h = ((h * 31) + c) % 9973;
        }

        return h % 7 == 0 ? 0 : (h % 6) + 1;
    }

    private static FestEmployerSeed E(string id, string company, string initials, string role, int openings, int slotsLeft, string days, int match, int desk) =>
        new(id, company, initials, role, openings, slotsLeft, days, match, $"Desk {desk}");
}
