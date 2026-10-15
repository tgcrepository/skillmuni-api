using System.Globalization;
using Cv.Domain;

namespace Cv.Application.Services;

/// <summary>
/// The "Here's what I found" screen (cv.api.ts CvSummary): one section per quest the CV can pre-fill (1–5), with
/// amber "Check" rows where the learner has to confirm or add something.
/// </summary>
public static class CvSummaryBuilder
{
    // Invariant culture: English month names and title case without ICU (images run in globalization-invariant mode).
    private static readonly CultureInfo English = CultureInfo.InvariantCulture;

    public static CvSummaryDto Build(CvExtract extract, string market, DateTime now)
    {
        var sections = new List<CvSectionDto>();
        Add(sections, NameSection(extract));
        Add(sections, ContactSection(extract, market));
        Add(sections, EducationSection(extract));
        Add(sections, SkillsSection(extract));
        Add(sections, WorkSection(extract, now));

        return new CvSummaryDto(
            sections.Count,
            extract.DetailsFound,
            sections.Sum(s => s.Fields.Count(f => f.Check is not null)),
            extract.Skills.Count(s => s.Category != "custom"),
            sections,
            extract.Skipped);
    }

    private static void Add(List<CvSectionDto> sections, CvSectionDto? section)
    {
        if (section is not null)
        {
            sections.Add(section);
        }
    }

    private static CvSectionDto? NameSection(CvExtract e) =>
        e.Name is null
            ? null
            : new CvSectionDto(1, "Your name", "2 found",
            [
                new("firstName", "First name", e.Name.First),
                new("lastName", "Last name", e.Name.Last),
            ]);

    private static CvSectionDto? ContactSection(CvExtract e, string market)
    {
        var fields = new List<CvFieldDto>();
        if (e.Contact.Email is { } email)
        {
            fields.Add(new("email", "Email", email));
        }

        if (e.Contact.Phone is { } phone)
        {
            fields.Add(new("mobile", "Mobile", DisplayPhone(phone)));
        }

        if (e.Languages.FirstOrDefault(l => l.Native) is { } home)
        {
            fields.Add(new("homeLanguage", "Home language", home.Name));
        }

        if (e.Contact.City is not null || e.Contact.Region is not null)
        {
            var area = string.Join(", ", new[] { e.Contact.City, e.Contact.Region }.Where(v => v is not null));
            var postalName = market == "ZA" ? "postal code" : "PIN code";
            fields.Add(new("area", "Area", area, e.Contact.PostalCode is null ? $"Found a {(e.Contact.City is null ? "region" : "city")} only — add your {postalName}" : null));
        }

        return fields.Count == 0 ? null : new CvSectionDto(2, "Contact & address", Badge(fields), fields);
    }

    private static CvSectionDto? EducationSection(CvExtract e)
    {
        if (e.Education.Count == 0)
        {
            return null;
        }

        var fields = e.Education.Take(3).Select((ed, i) =>
        {
            var value = string.Join(" · ", new[] { ed.Title, ed.Institute, ed.Year }.Where(v => !string.IsNullOrWhiteSpace(v)));
            return new CvFieldDto(i == 0 ? "level" : $"education{i + 1}", i == 0 ? "Highest level" : "Qualification", value, ed.Year is null ? "Year not found — when did you finish?" : null);
        }).ToList();
        return new CvSectionDto(3, "School & qualifications", Badge(fields), fields);
    }

    private static CvSectionDto? SkillsSection(CvExtract e)
    {
        if (e.Skills.Count == 0 && e.Languages.Count == 0)
        {
            return null;
        }

        var fields = new List<CvFieldDto>();
        if (e.Skills.Count > 0)
        {
            fields.Add(new("skillLevels", "Skill levels", "Guessed from your CV — you’ll confirm each one"));
        }

        if (e.Languages.Count > 0)
        {
            var unsure = e.Languages.FirstOrDefault(l => l.Fluency.Length == 0);
            var value = string.Join(" · ", e.Languages.Select(l => l.Fluency.Length == 0 ? l.Name : $"{l.Name} ({English.TextInfo.ToTitleCase(l.Fluency)})"));
            fields.Add(new("languages", "Languages", value, unsure is null ? null : $"How well do you speak {unsure.Name}?"));
        }

        var badge = $"{Count(e.Skills.Count, "skill")} · {Count(e.Languages.Count, "language")}";
        return new CvSectionDto(4, "Skills & languages", badge, fields, e.Skills.Select(s => s.Name).ToList());
    }

    private static CvSectionDto? WorkSection(CvExtract e, DateTime now)
    {
        if (e.Work.Count == 0)
        {
            return null;
        }

        var fields = e.Work.Select((job, i) => new CvFieldDto(
            $"job{i + 1}",
            $"Chapter {i + 1}",
            string.Join(" · ", new[]
            {
                job.Type == "internship" ? $"{job.Role} (internship)" : job.Role,
                job.Company,
                Period(job),
            }.Where(v => v.Length > 0)))).ToList();

        var months = e.Work.Sum(j => MonthsBetween(j.Start, j.Current ? Month(now) : j.End));
        var latestEnd = e.Work.Any(j => j.Current) ? null : e.Work.Select(j => j.End).Where(v => v is not null).Max();
        if (latestEnd is not null && MonthsBetween(latestEnd, Month(now)) > 3)
        {
            fields.Add(new("gap", "Gap", $"{MonthYear(Next(latestEnd))} – now · shown only to you"));
        }

        return new CvSectionDto(5, "Work history", $"{Count(e.Work.Count, "chapter")} · {Duration(months)}", fields);
    }

    private static string Badge(List<CvFieldDto> fields)
    {
        var checks = fields.Count(f => f.Check is not null);
        return checks == 0 ? $"{fields.Count} found" : $"{fields.Count} found · {checks} to check";
    }

    private static string Count(int n, string noun) => $"{n} {noun}{(n == 1 ? string.Empty : "s")}";

    private static string Period(CvJob job)
    {
        var start = job.Start is null ? null : MonthYear(job.Start);
        var end = job.Current ? "now" : job.End is null ? null : MonthYear(job.End);
        return start is null ? end ?? string.Empty : $"{start} – {end ?? "?"}";
    }

    private static string Duration(int months) => (months / 12, months % 12) switch
    {
        (0, var m) => $"{m} mos",
        (var y, 0) => $"{y} yr{(y == 1 ? string.Empty : "s")}",
        (var y, var m) => $"{y} yr{(y == 1 ? string.Empty : "s")} {m} mos",
    };

    private static string Month(DateTime d) => d.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static string Next(string yearMonth)
    {
        var d = Parse(yearMonth).AddMonths(1);
        return Month(d);
    }

    private static DateTime Parse(string yearMonth) =>
        DateTime.ParseExact(yearMonth, "yyyy-MM", CultureInfo.InvariantCulture);

    private static string MonthYear(string yearMonth) => Parse(yearMonth).ToString("MMM yyyy", English);

    /// <summary>Inclusive of both months: Jan–Jun is 6.</summary>
    private static int MonthsBetween(string? start, string? end)
    {
        if (start is null || end is null)
        {
            return 0;
        }

        var (a, b) = (Parse(start), Parse(end));
        return Math.Max(0, ((b.Year - a.Year) * 12) + b.Month - a.Month + 1);
    }

    /// <summary>"+27712345678" → "+27 71 234 5678"; "+919876543210" → "+91 98765 43210".</summary>
    internal static string DisplayPhone(string phone) => phone switch
    {
        _ when phone.StartsWith("+91", StringComparison.Ordinal) && phone.Length == 13 => $"+91 {phone[3..8]} {phone[8..]}",
        _ when phone.StartsWith("+27", StringComparison.Ordinal) && phone.Length == 12 => $"+27 {phone[3..5]} {phone[5..8]} {phone[8..]}",
        _ => phone,
    };
}
