using System.Globalization;
using System.Text.RegularExpressions;
using Cv.Domain;

namespace Cv.Application.Parsing;

/// <summary>Reads a CV's text into a <see cref="CvExtract"/>, dropping what Skillmuni never keeps.</summary>
public interface ICvInterpreter
{
    CvExtract Interpret(CvText text, string market);
}

/// <summary>
/// Rule-based reading of entry-level CVs: contact patterns per market, section headings, date ranges, a skill and
/// language vocabulary. Deterministic and explainable; an LLM-backed interpreter can replace it behind the same
/// interface once a provider is chosen (flagged).
/// </summary>
public sealed partial class CvInterpreter : ICvInterpreter
{
    private const int MaxJobs = 8;
    private const int MaxCustomSkills = 5;

    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    private static readonly string[] NotAName = ["curriculum", "vitae", "resume", "résumé", "cv", "profile", "email", "phone", "mobile", "address", "objective"];

    private enum Section
    {
        Header,
        Education,
        Work,
        Skills,
        Languages,
        Other,
    }

    public CvExtract Interpret(CvText text, string market)
    {
        var lines = text.Lines.Select(l => WhitespacePattern().Replace(l, " ").Trim()).Where(l => l.Length > 0).ToList();
        var sections = SplitSections(lines);
        var all = string.Join('\n', lines);

        var skipped = PrivateDetails(all);
        if (text.FromPhoto is false && all.Contains("photo", StringComparison.OrdinalIgnoreCase))
        {
            skipped.Add("photo");
        }

        return new CvExtract(
            FindName(sections[Section.Header].Concat(lines).Take(6)),
            FindContact(lines, market),
            FindLanguages(sections[Section.Languages].Count > 0 ? sections[Section.Languages] : lines, market),
            FindEducation(sections[Section.Education]),
            FindWork(sections[Section.Work]),
            FindSkills(all, sections[Section.Skills]),
            skipped.Distinct().ToList(),
            text.Locale);
    }

    private static Dictionary<Section, List<string>> SplitSections(List<string> lines)
    {
        var sections = Enum.GetValues<Section>().ToDictionary(s => s, _ => new List<string>());
        var current = Section.Header;
        foreach (var line in lines)
        {
            if (Heading(line) is { } heading)
            {
                current = heading;
                var rest = line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim();
                if (line.Contains(':', StringComparison.Ordinal) && rest.Length > 0)
                {
                    sections[current].Add(rest);
                }

                continue;
            }

            sections[current].Add(line);
        }

        return sections;
    }

    private static Section? Heading(string line)
    {
        var head = line.Split(':')[0].Trim().ToLowerInvariant();
        if (head.Length > 40 || head.Split(' ').Length > 4)
        {
            return null;
        }

        return head switch
        {
            _ when head.Contains("education", StringComparison.Ordinal) || head.Contains("qualification", StringComparison.Ordinal) || head.Contains("academic", StringComparison.Ordinal) => Section.Education,
            _ when head.Contains("experience", StringComparison.Ordinal) || head.Contains("employment", StringComparison.Ordinal) || head.Contains("work history", StringComparison.Ordinal) => Section.Work,
            _ when head.StartsWith("skills", StringComparison.Ordinal) || head.Contains("key skills", StringComparison.Ordinal) || head.Contains("competenc", StringComparison.Ordinal) => Section.Skills,
            _ when head.StartsWith("languages", StringComparison.Ordinal) || head == "language" => Section.Languages,
            _ when head is "references" or "hobbies" or "interests" or "declaration" or "personal details" or "personal information" or "achievements" => Section.Other,
            _ => null,
        };
    }

    private static PersonName? FindName(IEnumerable<string> candidates)
    {
        foreach (var line in candidates)
        {
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lower = line.ToLowerInvariant();
            if (words.Length is < 2 or > 4 || NotAName.Any(w => lower.Contains(w, StringComparison.Ordinal)) || !NamePattern().IsMatch(line))
            {
                continue;
            }

            var proper = words.Select(w => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(w.ToLowerInvariant())).ToArray();
            return new PersonName(proper[0], string.Join(' ', proper[1..]));
        }

        return null;
    }

    private static CvContact FindContact(List<string> lines, string market)
    {
        var all = string.Join('\n', lines);
        var email = EmailPattern().Match(all) is { Success: true } e ? e.Value.ToLowerInvariant() : null;
        var phone = market == "ZA" ? ZaPhone(all) : InPhone(all);

        string? city = null;
        string? region = null;
        if (CvVocabulary.Cities.TryGetValue(market, out var cities))
        {
            foreach (var (name, state) in cities)
            {
                if (Regex.IsMatch(all, $@"\b{Regex.Escape(name)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)))
                {
                    (city, region) = (name, state);
                    break;
                }
            }
        }

        // An Indian PIN on a line that also has words (an address), never a bare number that could be a phone part.
        var postal = market == "IN"
            ? lines.Select(l => PinPattern().Match(l)).FirstOrDefault(m => m.Success && LettersPattern().IsMatch(m.Result("$`")))?.Value
            : null;

        return new CvContact(email, phone, city, region, postal);
    }

    private static string? InPhone(string text) =>
        InPhonePattern().Match(text) is { Success: true } m ? "+91" + m.Groups["n"].Value.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal) : null;

    private static string? ZaPhone(string text) =>
        ZaPhonePattern().Match(text) is { Success: true } m ? "+27" + DigitsOnly(m.Groups["n"].Value) : null;

    private static string DigitsOnly(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

    private static List<string> PrivateDetails(string text)
    {
        var skipped = new List<string>();
        if (AadhaarPattern().IsMatch(text) || PanPattern().IsMatch(text) || SaIdPattern().IsMatch(text))
        {
            skipped.Add("ID number");
        }

        if (MaritalPattern().IsMatch(text))
        {
            skipped.Add("marital status");
        }

        if (DobPattern().IsMatch(text))
        {
            skipped.Add("date of birth");
        }

        if (ReligionPattern().IsMatch(text))
        {
            skipped.Add("religion");
        }

        return skipped;
    }

    private static List<CvLanguage> FindLanguages(List<string> lines, string market)
    {
        var known = CvVocabulary.Languages.GetValueOrDefault(market, CvVocabulary.Languages["IN"]);
        var found = new List<CvLanguage>();
        foreach (var line in lines)
        {
            foreach (var part in line.Split([',', ';', '|', '·', '•'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var name = known.FirstOrDefault(k => Regex.IsMatch(part, $@"\b{Regex.Escape(k)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)))
                    ?? CvVocabulary.LanguageAliases.FirstOrDefault(a => Regex.IsMatch(part, $@"\b{Regex.Escape(a.Key)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200))).Value;
                if (name is null || found.Any(f => f.Name == name))
                {
                    continue;
                }

                var lower = part.ToLowerInvariant();
                var native = lower.Contains("native", StringComparison.Ordinal) || lower.Contains("mother tongue", StringComparison.Ordinal) || lower.Contains("home language", StringComparison.Ordinal);
                var fluency = native ? "native"
                    : lower.Contains("fluent", StringComparison.Ordinal) || lower.Contains("proficient", StringComparison.Ordinal) ? "fluent"
                    : lower.Contains("basic", StringComparison.Ordinal) || lower.Contains("beginner", StringComparison.Ordinal) ? "basic"
                    : lower.Contains("intermediate", StringComparison.Ordinal) || lower.Contains("conversational", StringComparison.Ordinal) ? "conversational"
                    : string.Empty;
                found.Add(new CvLanguage(name, fluency, native));
            }
        }

        return found;
    }

    private static List<CvEducation> FindEducation(List<string> lines)
    {
        var result = new List<CvEducation>();
        foreach (var line in lines)
        {
            var lower = " " + line.ToLowerInvariant() + " ";
            var level = lower switch
            {
                _ when PostgradPattern().IsMatch(lower) => "postgraduate",
                _ when GradPattern().IsMatch(lower) => "graduate",
                _ when DiplomaPattern().IsMatch(lower) => "diploma",
                _ when Class12Pattern().IsMatch(lower) => "class12",
                _ when Class10Pattern().IsMatch(lower) => "class10",
                _ when CertificatePattern().IsMatch(lower) => "certificate",
                _ => null,
            };
            if (level is null)
            {
                continue;
            }

            var parts = line.Split([",", "|", "·", "–", "—", " - "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var institute = parts.FirstOrDefault(p => InstitutePattern().IsMatch(p));
            var year = YearPattern().Matches(line).LastOrDefault()?.Value;
            var title = YearPattern().Replace(parts[0], string.Empty).Trim(' ', '(', ')', '-');
            result.Add(new CvEducation(level, title.Length > 0 ? title : line, institute == parts[0] ? null : institute, year));
        }

        return result;
    }

    private static List<CvJob> FindWork(List<string> lines)
    {
        var jobs = new List<CvJob>();
        for (var i = 0; i < lines.Count && jobs.Count < MaxJobs; i++)
        {
            var range = DateRangePattern().Match(lines[i]);
            if (!range.Success)
            {
                continue;
            }

            // "Role, Company, Jan 2024 – Jun 2024", or the role and company on the line before the dates.
            var heading = lines[i].Remove(range.Index, range.Length).Trim(' ', ',', '|', '·', '-', '–', '(', ')');
            if (heading.Length < 3 && i > 0)
            {
                heading = lines[i - 1];
            }

            var (role, company) = RoleAndCompany(heading);
            if (role.Length == 0)
            {
                continue;
            }

            var end = range.Groups["end"].Value;
            var current = CurrentPattern().IsMatch(end);
            var context = (heading + " " + (i + 1 < lines.Count ? lines[i + 1] : string.Empty)).ToLowerInvariant();
            var (type, unsure) = context switch
            {
                _ when context.Contains("intern", StringComparison.Ordinal) => ("internship", false),
                _ when context.Contains("part-time", StringComparison.Ordinal) || context.Contains("part time", StringComparison.Ordinal) => ("part_time", false),
                _ when context.Contains("freelance", StringComparison.Ordinal) => ("freelance", false),
                _ when context.Contains("volunteer", StringComparison.Ordinal) => ("volunteer", false),
                _ when context.Contains("full-time", StringComparison.Ordinal) || context.Contains("full time", StringComparison.Ordinal) => ("full_time", false),
                _ => ("full_time", true),
            };
            jobs.Add(new CvJob(role, company, YearMonth(range.Groups["start"].Value), current ? null : YearMonth(end), current, type, unsure));
        }

        return jobs;
    }

    private static (string Role, string Company) RoleAndCompany(string heading)
    {
        var at = Regex.Split(heading, @"\s+(?:at|@)\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
        var parts = at.Length > 1
            ? at
            : heading.Split([",", "|", "·", "–", "—", " - "], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var role = parts.Length > 0 ? parts[0].Trim() : string.Empty;
        var company = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return (Clean(role), Clean(company));
    }

    private static string Clean(string value) =>
        ParenPattern().Replace(value, string.Empty).Trim(' ', ',', '-', '–');

    /// <summary>"Jan 2024" → "2024-01"; "2022" → "2022-01".</summary>
    private static string? YearMonth(string value)
    {
        var year = YearPattern().Match(value);
        if (!year.Success)
        {
            return null;
        }

        var lower = value.ToLowerInvariant();
        var month = Array.FindIndex(Months, m => lower.Contains(m, StringComparison.Ordinal)) + 1;
        if (month == 0 && NumericMonthPattern().Match(value) is { Success: true } numeric)
        {
            month = int.Parse(numeric.Groups["m"].Value, CultureInfo.InvariantCulture);
        }

        return $"{year.Value}-{Math.Max(month, 1).ToString("00", CultureInfo.InvariantCulture)}";
    }

    private static List<CvSkill> FindSkills(string all, List<string> skillLines)
    {
        var found = new List<CvSkill>();
        foreach (var term in CvVocabulary.Skills)
        {
            var phrases = term.Synonyms.Prepend(term.Name);
            var match = phrases.Select(p => Regex.Match(all, $@"(?<![\w-]){Regex.Escape(p)}(?![\w-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)))
                .FirstOrDefault(m => m.Success);
            if (match is null)
            {
                continue;
            }

            var line = all[Math.Max(0, all.LastIndexOf('\n', match.Index) + 1)..].Split('\n')[0].ToLowerInvariant();
            var level = line.Contains("advanced", StringComparison.Ordinal) || line.Contains("expert", StringComparison.Ordinal) ? 3
                : line.Contains("basic", StringComparison.Ordinal) || line.Contains("beginner", StringComparison.Ordinal) ? 1
                : 2;
            found.Add(new CvSkill(term.Id, term.Name, term.Category, level));
        }

        // Anything else listed under "Skills" is kept as a custom skill for the learner to confirm.
        var custom = skillLines
            .SelectMany(l => l.Split([',', ';', '|', '·', '•'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(s => s.Trim('-', '*', ' '))
            .Where(s => s.Length is >= 2 and <= 40 && s.Split(' ').Length <= 3)
            .Where(s => !found.Any(f => string.Equals(f.Name, s, StringComparison.OrdinalIgnoreCase))
                && !CvVocabulary.Skills.Any(t => t.Synonyms.Prepend(t.Name).Any(p => string.Equals(p, s, StringComparison.OrdinalIgnoreCase))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxCustomSkills)
            .Select(s => new CvSkill($"custom-{Slug(s)}", s, "custom", 0));
        found.AddRange(custom);
        return found;
    }

    private static string Slug(string value) => SlugPattern().Replace(value.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"^[\p{L}][\p{L}'.-]*(?: [\p{L}][\p{L}'.-]*){1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<![\d])(?:\+?91[\s-]?|0)?(?<n>[6-9]\d{4}[\s-]?\d{5})(?![\d])", RegexOptions.CultureInvariant)]
    private static partial Regex InPhonePattern();

    [GeneratedRegex(@"(?<![\d])(?:\+?27[\s-]?|0)(?<n>[6-8]\d[\s-]?\d{3}[\s-]?\d{4})(?![\d])", RegexOptions.CultureInvariant)]
    private static partial Regex ZaPhonePattern();

    [GeneratedRegex(@"(?<![\d])[1-9]\d{5}(?![\d])", RegexOptions.CultureInvariant)]
    private static partial Regex PinPattern();

    [GeneratedRegex(@"[A-Za-z]{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex LettersPattern();

    [GeneratedRegex(@"(?<![\d+])[2-9]\d{3}[\s-]?\d{4}[\s-]?\d{4}(?![\d])", RegexOptions.CultureInvariant)]
    private static partial Regex AadhaarPattern();

    [GeneratedRegex(@"\b[A-Z]{5}\d{4}[A-Z]\b", RegexOptions.CultureInvariant)]
    private static partial Regex PanPattern();

    [GeneratedRegex(@"(?<![\d])\d{13}(?![\d])", RegexOptions.CultureInvariant)]
    private static partial Regex SaIdPattern();

    [GeneratedRegex(@"\bmarital\b|\bmarried\b|\bunmarried\b|\bsingle\b|\bdivorced\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MaritalPattern();

    [GeneratedRegex(@"\bdate of birth\b|\bd\.?o\.?b\b|\bborn on\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DobPattern();

    [GeneratedRegex(@"\breligion\b|\bcaste\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReligionPattern();

    [GeneratedRegex(@"\b(m\.?\s?com|m\.?\s?sc|m\.?\s?a|mba|mca|m\.?\s?tech|master'?s?|post ?graduate|pgdm)\b", RegexOptions.CultureInvariant)]
    private static partial Regex PostgradPattern();

    [GeneratedRegex(@"\b(b\.?\s?com|b\.?\s?sc|b\.?\s?a|bba|bca|b\.?\s?tech|b\.?\s?e|bachelor'?s?|degree|graduat(e|ion))\b", RegexOptions.CultureInvariant)]
    private static partial Regex GradPattern();

    [GeneratedRegex(@"\b(diploma|iti|polytechnic)\b", RegexOptions.CultureInvariant)]
    private static partial Regex DiplomaPattern();

    [GeneratedRegex(@"\b(class 12|class xii|12th|hsc|intermediate|matric|nsc|grade 12|puc)\b", RegexOptions.CultureInvariant)]
    private static partial Regex Class12Pattern();

    [GeneratedRegex(@"\b(class 10|class x|10th|ssc|sslc|grade 10)\b", RegexOptions.CultureInvariant)]
    private static partial Regex Class10Pattern();

    [GeneratedRegex(@"\b(certificate|certification|nqf|nsqf|course)\b", RegexOptions.CultureInvariant)]
    private static partial Regex CertificatePattern();

    [GeneratedRegex(@"school|college|university|institute|academy|vidyalaya|polytechnic|\biti\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InstitutePattern();

    [GeneratedRegex(@"\b(19[6-9]\d|20[0-4]\d)\b", RegexOptions.CultureInvariant)]
    private static partial Regex YearPattern();

    [GeneratedRegex(
        @"(?<start>(?:(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\.?\s+|\d{1,2}/)?(?:19|20)\d{2})\s*(?:-|–|—|to)\s*(?<end>present|current|now|till date|(?:(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)[a-z]*\.?\s+|\d{1,2}/)?(?:19|20)\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DateRangePattern();

    [GeneratedRegex(@"present|current|now|till date", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CurrentPattern();

    [GeneratedRegex(@"(?<m>\d{1,2})/(?:19|20)\d{2}", RegexOptions.CultureInvariant)]
    private static partial Regex NumericMonthPattern();

    [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex ParenPattern();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
