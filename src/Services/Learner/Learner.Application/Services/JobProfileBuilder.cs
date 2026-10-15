using System.Globalization;
using System.Text;
using Learner.Application.Contracts;
using Learner.Domain;

namespace Learner.Application.Services;

/// <summary>
/// Builds the Job Profile (A-262: one record, built from the 8 quests; no second form) and its CV suggestions. Pure
/// mapping over the learner, their quest answers and the Job Profile settings.
/// </summary>
internal static class JobProfileBuilder
{
    public const string WhoDis = "who-dis";
    public const string HitMeUp = "hit-me-up";
    public const string BigBrain = "big-brain-stuff";
    public const string Flex = "your-flex-era";
    public const string WorkLore = "work-lore";
    public const string Arc = "pick-your-arc";
    public const string Receipts = "drop-the-receipts";
    public const string VibeCheck = "vibe-check";

    public const string NeverShown = "ID number, full address";

    public const string InvitesOff = "Invites off";

    private const int SignatureMoves = 3;

    private static readonly string[] RadarCategories = ["people", "business", "data", "creative", "tech"];

    /// <summary>Section → the quest step that collected it (job-profile.handlers.ts SOURCES).</summary>
    public static readonly IReadOnlyDictionary<string, QuestRefDto> Sources = new Dictionary<string, QuestRefDto>(StringComparer.Ordinal)
    {
        ["about"] = new(WhoDis, 1, "Who Dis?"),
        ["contact"] = new(HitMeUp, 1, "Hit Me Up"),
        ["work"] = new(WorkLore, 1, "Work Lore"),
        ["education"] = new(BigBrain, 1, "Big Brain Stuff"),
        ["languages"] = new(Flex, 2, "Your Flex Era"),
        ["skills"] = new(Flex, 1, "Your Flex Era"),
        ["certificates"] = new(Receipts, 1, "Drop the Receipts"),
        ["lookingFor"] = new(Arc, 1, "Pick Your Arc"),
    };

    private static readonly Dictionary<string, string> LevelLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["10th"] = "10th",
        ["12th"] = "12th",
        ["iti"] = "ITI",
        ["diploma"] = "Diploma",
        ["graduate"] = "Graduate",
        ["postgraduate"] = "Postgraduate",
        ["phd"] = "PhD",
        ["grade9"] = "Grade 9",
        ["matric"] = "Matric",
        ["tvet"] = "TVET certificate",
    };

    /// <summary>South Africa's own names for two levels (education.ts LEVELS.ZA).</summary>
    private static readonly Dictionary<string, string> ZaLevelLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["graduate"] = "Bachelor's degree",
        ["postgraduate"] = "Honours / Master's",
    };

    private static readonly Dictionary<string, string> LookingForLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["full_time"] = "Full-time",
        ["part_time"] = "Part-time",
        ["internship"] = "Internship",
        ["apprenticeship"] = "Apprenticeship",
        ["learnership"] = "Learnership",
        ["freelance"] = "Freelance",
        ["short_course"] = "Short course",
    };

    /// <summary>arc.ts SHIFTS.</summary>
    private static readonly Dictionary<string, string> ShiftLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["day"] = "Day",
        ["night"] = "Night (US/UK shift)",
        ["rotational"] = "Rotational",
        ["weekends"] = "Weekends",
    };

    /// <summary>arc.ts NOTICE.ZA (India's values are a number of days).</summary>
    private static readonly Dictionary<string, string> ZaNoticeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["7"] = "1 week",
        ["14"] = "2 weeks",
        ["30"] = "1 month",
        ["60"] = "2 months +",
    };

    /// <summary>arc.ts SALARY: the options' own labels.</summary>
    private static readonly Dictionary<string, string> SalaryLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IN:0-2"] = "₹0 – 2 LPA",
        ["IN:2-3"] = "₹2 – 3 LPA",
        ["IN:3-4.5"] = "₹3 – 4.5 LPA",
        ["IN:4.5-6"] = "₹4.5 – 6 LPA",
        ["IN:6-10"] = "₹6 – 10 LPA",
        ["IN:10+"] = "₹10 LPA +",
        ["ZA:0-5k"] = "R0 – R5 000 / month",
        ["ZA:5-8k"] = "R5 000 – R8 000 / month",
        ["ZA:8-12k"] = "R8 000 – R12 000 / month",
        ["ZA:12-20k"] = "R12 000 – R20 000 / month",
        ["ZA:20k+"] = "R20 000 + / month",
    };

    /// <summary>Quest 7 receipt keys (q7-receipts.ts): the highest education, timeline entries, trophies, work chapters.</summary>
    private const string EduReceipt = "edu";

    public sealed record Input(
        LearnerProfile Learner,
        IReadOnlyDictionary<string, string> Answers,
        JobProfileSettings Settings,
        ProfileProgressDto Progress,
        DateTime UpdatedAt);

    public static JobProfileDto Build(Input input)
    {
        var learner = input.Learner;
        var contact = Answer(input, HitMeUp);
        var education = Answer(input, BigBrain);
        var flex = Answer(input, Flex);
        var lore = Answer(input, WorkLore);
        var arc = Answer(input, Arc);
        var vibe = Answer(input, VibeCheck);
        var receipts = Answer(input, Receipts).Map("receipts");

        var skills = Skills(flex);
        var radar = Radar(flex);
        var work = Work(lore, contact, receipts);
        var schools = Education(education, receipts, learner.Market);
        var certificates = Certificates(flex, receipts);
        var verified = work.Count(w => w.Proof == "verified") + schools.Count(e => e.Proof == "verified") + certificates.Count(c => c.Status == "verified");
        var city = contact.Str("city");
        var roles = arc.Strings("roles");

        // The deck's first three skills with a level are the signature moves (q4-deck.ts SIGNATURES, quest-page.ts).
        var signature = skills.Where(s => s.Signature).ToList();

        // Quest 8 "invites" (q8-vibe.ts): employers may invite by email, and by WhatsApp when Quest 2 opted in.
        var invites = new List<string>();
        if (vibe.Bool("invites") ?? true)
        {
            invites.Add("Email");
            if (contact.Bool("whatsapp") == true)
            {
                invites.Add("WhatsApp");
            }
        }

        return new JobProfileDto(
            learner.FullName,
            ProfileFormat.Initials(learner),
            Headline(roles, lore),
            learner.ArchetypeName is { } archetype ? new JobArchetypeDto(archetype, Strongest(radar)) : null,
            radar,
            signature.Select(s => new SignatureSkillDto(s.Name, s.Power ?? 0)).ToList(),
            new JobProfileCountsDto(skills.Count, flex.Items("languages").Count, certificates.Count),
            input.Progress.Quests,
            new JobProfileStrengthDto(input.Progress.Percent, StrengthLabel(input.Progress.Percent), JobMatches: 0, QuickWins(skills, input.Progress)),
            new JobProfileContactDto(
                ProfileFormat.MaskMobile(learner.Phone ?? contact.Str("phone")),
                learner.Phone is not null || contact.Bool("phoneVerified") == true,
                ProfileFormat.MaskEmail(learner.Email ?? NullIfEmpty(contact.Str("email"))),
                Location(contact, withPostal: true),
                city,
                vibe.Bool("discoverable") ?? true,
                invites.Count == 0 ? InvitesOff : string.Join(" · ", invites),
                NeverShown),
            About(input, learner, schools, work, flex, arc, city),
            work,
            schools,
            flex.Items("languages").Select(Language).Where(l => l.Name.Length > 0).ToList(),
            skills,
            certificates,
            LookingFor(arc, contact, learner.Market),
            VerifyCode(input.Settings),
            verified,
            DateTime.SpecifyKind(input.UpdatedAt, DateTimeKind.Utc),
            [],
            Sources);
    }

    /// <summary>
    /// The CV's contact block. The quests don't ask for LinkedIn, so it comes from the latest CV read (the CV pre-fill
    /// of Quest 2) when the CV had one.
    /// </summary>
    public static CvContactDto Contact(LearnerProfile learner, IReadOnlyDictionary<string, string> answers, JobProfileSettings? settings = null)
    {
        var contact = QuestAnswerView.Parse(answers.GetValueOrDefault(HitMeUp));
        var arc = QuestAnswerView.Parse(answers.GetValueOrDefault(Arc));
        var cvContact = new QuestAnswerView(QuestAnswerView.Parse(settings?.CvPrefillJson).Raw[HitMeUp] as System.Text.Json.Nodes.JsonObject);
        var linkedin = contact.Str("linkedin", "linkedIn") is { Length: > 0 } own ? own : cvContact.Str("linkedin", "linkedIn");
        return new CvContactDto(
            ProfileFormat.DisplayPhone(learner.Phone ?? NullIfEmpty(contact.Str("phone"))),
            learner.Email ?? contact.Str("email"),
            Location(contact, withPostal: false),
            linkedin.Length == 0 ? null : linkedin,
            ExpectedPay(arc.Str("salary"), learner.Market));
    }

    public static string ShareUrl(string baseUrl, LearnerProfile learner, JobProfileSettings settings)
    {
        var name = Slug(learner.FirstName, 30);
        var path = name.Length == 0 ? settings.ShareCode : $"{name}-{settings.ShareCode}";
        return $"{baseUrl.TrimEnd('/')}/cv/{path}";
    }

    public static string VerifyCode(JobProfileSettings settings) =>
        settings.ShareCode.Length >= 5 ? settings.ShareCode[..5].ToUpperInvariant() : settings.ShareCode.ToUpperInvariant();

    /// <summary>
    /// P5: what the latest CV read has that the record doesn't: jobs, skills and certificates to add, and contact
    /// details that differ from the verified ones (suggested "keep", never overwritten, A-207).
    /// </summary>
    public static IReadOnlyList<(CvChangeDto Change, string Slug, string ListKey, QuestAnswerView? Item)> Changes(
        LearnerProfile learner,
        IReadOnlyDictionary<string, string> answers,
        JobProfileSettings settings)
    {
        var prefill = QuestAnswerView.Parse(settings.CvPrefillJson);
        var changes = new List<(CvChangeDto, string, string, QuestAnswerView?)>();
        if (prefill.IsEmpty)
        {
            return changes;
        }

        var cvLore = new QuestAnswerView(prefill.Raw[WorkLore] as System.Text.Json.Nodes.JsonObject);
        var cvFlex = new QuestAnswerView(prefill.Raw[Flex] as System.Text.Json.Nodes.JsonObject);
        var cvContact = new QuestAnswerView(prefill.Raw[HitMeUp] as System.Text.Json.Nodes.JsonObject);
        var lore = QuestAnswerView.Parse(answers.GetValueOrDefault(WorkLore));
        var flex = QuestAnswerView.Parse(answers.GetValueOrDefault(Flex));

        var jobs = lore.Items("chapters").Select(c => Key(c.Str("role"), c.Str("company"))).ToHashSet(StringComparer.Ordinal);
        foreach (var chapter in cvLore.Items("chapters"))
        {
            var (role, company) = (chapter.Str("role"), chapter.Str("company"));
            if (role.Length == 0 || jobs.Contains(Key(role, company)))
            {
                continue;
            }

            var title = company.Length == 0 ? role : $"{role} · {company}";
            changes.Add((new CvChangeDto($"job-{Slug($"{company} {role}", 60)}", "job", title, $"New job · {Period(chapter, withLength: false)}", "add"), WorkLore, "chapters", chapter));
        }

        var known = flex.Items("skills").Select(s => s.Str("name").ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
        foreach (var skill in cvFlex.Items("skills"))
        {
            var name = skill.Str("name");
            if (name.Length > 0 && known.Add(name.ToUpperInvariant()))
            {
                changes.Add((new CvChangeDto($"skill-{Slug(name, 60)}", "skill", name, "New skill", "add"), Flex, "skills", skill));
            }
        }

        var trophies = flex.Items("trophies").Select(t => t.Str("name", "title").ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
        foreach (var trophy in cvFlex.Items("trophies"))
        {
            var name = trophy.Str("name", "title");
            if (name.Length > 0 && trophies.Add(name.ToUpperInvariant()))
            {
                var issuer = trophy.Str("issuer");
                var title = issuer.Length == 0 ? name : $"{name} · {issuer}";
                changes.Add((new CvChangeDto($"cert-{Slug(name, 60)}", "certificate", title, "New certificate · needs proof to verify", "add"), Flex, "trophies", trophy));
            }
        }

        var cvEmail = cvContact.Str("email");
        if (cvEmail.Length > 0 && learner.Email is { } email && !string.Equals(cvEmail, email, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add((new CvChangeDto($"email-{Slug(cvEmail, 60)}", "contact", $"Email: {cvEmail}", "Different from your verified email", "keep"), HitMeUp, string.Empty, null));
        }

        var cvPhone = Digits(cvContact.Str("phone"));
        if (cvPhone.Length >= 10 && learner.Phone is { } phone && !Digits(phone).EndsWith(cvPhone[^10..], StringComparison.Ordinal))
        {
            changes.Add((new CvChangeDto($"phone-{cvPhone[^4..]}", "contact", $"Mobile: {cvContact.Str("phone")}", "Different from your verified mobile", "keep"), HitMeUp, string.Empty, null));
        }

        return changes;
    }

    public static string Slug(string text, int max)
    {
        var builder = new StringBuilder();
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length <= max ? slug : slug[..max].TrimEnd('-');
    }

    private static QuestAnswerView Answer(Input input, string slug) => QuestAnswerView.Parse(input.Answers.GetValueOrDefault(slug));

    private static string Key(string role, string company) => $"{role.ToUpperInvariant()}|{company.ToUpperInvariant()}";

    private static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static string Headline(IReadOnlyList<string> roles, QuestAnswerView lore)
    {
        if (roles.Count > 0)
        {
            return string.Join(" · ", roles.Take(2));
        }

        return lore.Items("chapters").FirstOrDefault()?.Str("role") ?? string.Empty;
    }

    /// <summary>
    /// Quest 4's deck in the learner's order (q4-deck.ts: drag to reorder, "top 3 are your signature moves"). A card
    /// with level 0 has no level yet and can't be a signature move (quest-page.ts filters on the level).
    /// </summary>
    private static List<SkillItemDto> Skills(QuestAnswerView flex)
    {
        var signatures = 0;
        return flex.Items("skills")
            .Select(s => (Name: s.Str("name"), Level: s.Int("level")))
            .Where(s => s.Name.Length > 0)
            .Select(s =>
            {
                var power = s.Level is >= 1 and <= 3 ? s.Level : (int?)null;
                var signature = power is not null && signatures < SignatureMoves;
                if (signature)
                {
                    signatures++;
                }

                return new SkillItemDto(s.Name, power, signature);
            })
            .ToList();
    }

    /// <summary>
    /// A server approximation of the Player Card radar: each category's share of the deck's skill levels (the class
    /// title and the card's own drawing stay client-side, D-17). Null without skills.
    /// </summary>
    private static RadarStatsDto? Radar(QuestAnswerView flex)
    {
        var skills = flex.Items("skills");
        if (skills.Count == 0)
        {
            return null;
        }

        var sums = RadarCategories.ToDictionary(c => c, c => skills.Where(s => string.Equals(s.Str("category"), c, StringComparison.OrdinalIgnoreCase)).Sum(s => Math.Clamp(s.Int("level"), 1, 3)), StringComparer.Ordinal);
        var top = Math.Max(1, sums.Values.Max());
        int Share(string category) => (int)Math.Round(sums[category] * 100.0 / top, MidpointRounding.AwayFromZero);
        return new RadarStatsDto(Share("people"), Share("business"), Share("data"), Share("creative"), Share("tech"));
    }

    private static string Strongest(RadarStatsDto? radar)
    {
        if (radar is null)
        {
            return string.Empty;
        }

        var scores = new (string Name, int Score)[]
        {
            ("People", radar.People), ("Business", radar.Business), ("Data", radar.Data), ("Creative", radar.Creative), ("Tech", radar.Tech),
        };
        return scores.OrderByDescending(s => s.Score).First().Name;
    }

    /// <summary>Quest 5 chapters (work.ts <c>Chapter</c>). A chapter has no city of its own, so Quest 2's city is shown.</summary>
    private static List<WorkItemDto> Work(QuestAnswerView lore, QuestAnswerView contact, IReadOnlyDictionary<string, QuestAnswerView> receipts) =>
        lore.Items("chapters")
            .Select((c, i) =>
            {
                var company = c.Str("company");
                var description = c.Str("description");
                var id = c.Str("id");
                return new WorkItemDto(
                    id.Length > 0 ? id : $"chapter-{i + 1}",
                    Initials(company.Length > 0 ? company : c.Str("role")),
                    c.Str("role"),
                    company,
                    contact.Str("city"),
                    Period(c, withLength: true),
                    description.Length > 0 ? [description] : [],
                    Proof(c, receipts, id.Length > 0 ? $"ch-{id}" : null));
            })
            .Where(w => w.Role.Length > 0)
            .ToList();

    private static string LevelLabel(string level, string market) =>
        market == "ZA" && ZaLevelLabels.TryGetValue(level, out var za) ? za : LevelLabels.GetValueOrDefault(level, level);

    /// <summary>
    /// Quest 3: the highest level (parts 1–2, q3-details.ts) and the optional timeline (education.ts
    /// <c>TimelineEntry</c>): school levels with a board and, for 12th, a stream; courses by name; other
    /// qualifications (<c>level: "education"</c>) by degree or level, with a specialisation.
    /// </summary>
    private static List<EducationItemDto> Education(QuestAnswerView education, IReadOnlyDictionary<string, QuestAnswerView> receipts, string market)
    {
        var items = new List<EducationItemDto>();
        var level = education.Str("level");
        if (level.Length > 0 && !string.Equals(level, "none", StringComparison.OrdinalIgnoreCase))
        {
            var title = education.Str("degree") is { Length: > 0 } degree ? degree : LevelLabel(level, market);
            var branch = education.Str("branch");
            var school = education.Str("institute", "board");
            var detail = new List<string>();
            if (string.Equals(education.Str("status"), "studying", StringComparison.OrdinalIgnoreCase))
            {
                detail.Add(education.Str("studyYear") is { Length: > 0 } year ? $"Studying · year {year}" : "Studying");
            }
            else if (education.Str("year") is { Length: > 0 } year)
            {
                detail.Add(year);
            }

            if (Score(education) is { Length: > 0 } score)
            {
                detail.Add(score);
            }

            items.Add(new EducationItemDto(
                "highest",
                Initials(school.Length > 0 ? school : title),
                branch.Length > 0 ? $"{title} · {branch}" : title,
                school,
                string.Join(" · ", detail),
                Proof(education, receipts, EduReceipt)));
        }

        items.AddRange(education.Items("timeline").Select((t, i) =>
        {
            var entryLevel = t.Str("level");
            var title = entryLevel switch
            {
                "course" => t.Str("name"),
                "education" => t.Str("degree") is { Length: > 0 } degree ? degree : LevelLabel(t.Str("qual"), market),
                _ => LevelLabel(entryLevel, market),
            };
            var extra = entryLevel switch
            {
                "course" => string.Empty,
                "education" => t.Str("branch"),
                _ => t.Str("stream"),
            };
            if (title.Length > 0 && extra.Length > 0)
            {
                title = $"{title} · {extra}";
            }

            var school = t.Str("institute", "board");
            var detail = string.Join(" · ", new[] { t.Str("year"), Score(t) }.Where(x => x.Length > 0));
            var id = t.Str("id");
            return new EducationItemDto(
                id.Length > 0 ? id : $"timeline-{i + 1}",
                Initials(school.Length > 0 ? school : title),
                title,
                school,
                detail,
                Proof(t, receipts, id.Length > 0 ? $"tl-{id}" : null));
        }).Where(e => e.Title.Length > 0));
        return items;
    }

    private static string Score(QuestAnswerView education)
    {
        var score = education.Str("score");
        if (score.Length == 0)
        {
            return string.Empty;
        }

        return education.Str("scoreKind").ToLowerInvariant() switch
        {
            "cgpa" => $"CGPA {score}",
            "grade" => $"Grade {score}",
            _ => score.EndsWith('%') ? score : $"{score}%",
        };
    }

    /// <summary>Quest 4 trophies (flex.ts <c>Trophy</c>): issued on YYYY-MM, shown as the year.</summary>
    private static List<CertificateItemDto> Certificates(QuestAnswerView flex, IReadOnlyDictionary<string, QuestAnswerView> receipts) =>
        flex.Items("trophies")
            .Select((t, i) =>
            {
                var issuer = t.Str("issuer");
                var year = t.Str("issuedOn");
                year = year.Length >= 4 ? year[..4] : year;
                var id = t.Str("id");
                var status = Proof(t, receipts, id.Length > 0 ? $"tr-{id}" : null);
                return new CertificateItemDto(
                    id.Length > 0 ? id : $"trophy-{i + 1}",
                    t.Str("name"),
                    string.Join(" · ", new[] { issuer, year }.Where(x => x.Length > 0)),
                    status,
                    status == "checking" ? CheckingEta : null);
            })
            .Where(c => c.Title.Length > 0)
            .ToList();

    /// <summary>q7-receipts.ts: "Checking · usually under 48 hours".</summary>
    private const string CheckingEta = "48 h";

    /// <summary>
    /// Proof status per item: verified when the item itself is (a DigiLocker import, a trophy whose credential link
    /// was checked), else the status of its Quest 7 receipt (<c>checking</c> after an upload, <c>verified</c> once
    /// reviewed), else none.
    /// </summary>
    private static string Proof(QuestAnswerView item, IReadOnlyDictionary<string, QuestAnswerView> receipts, string? receiptKey)
    {
        if (item.Bool("verified") == true || string.Equals(item.Str("source"), "digilocker", StringComparison.OrdinalIgnoreCase))
        {
            return "verified";
        }

        var receipt = receiptKey is null ? null : receipts.GetValueOrDefault(receiptKey);
        return receipt?.Str("status").ToLowerInvariant() switch
        {
            "verified" => "verified",
            "checking" => "checking",
            _ => "none",
        };
    }

    private static LanguageItemDto Language(QuestAnswerView language)
    {
        var skills = new List<string>();
        if (language.Bool("speak") == true)
        {
            skills.Add("Speak");
        }

        if (language.Bool("read") == true)
        {
            skills.Add("Read");
        }

        if (language.Bool("write") == true)
        {
            skills.Add("Write");
        }

        return new LanguageItemDto(language.Str("name"), string.Join(" · ", skills), Capitalise(language.Str("fluency")));
    }

    private static List<LookingForRowDto> LookingFor(QuestAnswerView arc, QuestAnswerView contact, string market)
    {
        var rows = new List<LookingForRowDto>();
        void Add(string label, string value, bool? good = null, bool? isPrivate = null)
        {
            if (value.Length > 0)
            {
                rows.Add(new LookingForRowDto(label, value, good, isPrivate));
            }
        }

        Add("Looking for", string.Join(" · ", arc.Strings("lookingFor").Select(v => LookingForLabels.GetValueOrDefault(v, Humanise(v)))));
        Add("Roles", string.Join(" · ", arc.Strings("roles")));
        Add("Where", string.Join(" · ", arc.Strings("where").Select(w => w.ToLowerInvariant() switch
        {
            "near" => contact.Str("city") is { Length: > 0 } city ? $"{city} + 25 km" : "Near me",
            "region" => contact.Str("region") is { Length: > 0 } region ? $"Anywhere in {region}" : "Anywhere in my state",
            "relocate" => "open to relocating",
            "remote" => "open to remote",
            _ => Humanise(w),
        })));
        Add("Shifts", string.Join(" · ", arc.Strings("shifts").Select(s => ShiftLabels.GetValueOrDefault(s, Humanise(s)))));
        var notice = arc.Str("notice");
        var immediate = string.Equals(notice, "immediate", StringComparison.OrdinalIgnoreCase);
        Add("Can join", immediate ? "Immediately" : NoticeLabel(notice, market), immediate ? true : null);
        Add("Expected pay", ExpectedPay(arc.Str("salary"), market), isPrivate: true);
        return rows;
    }

    private static JobProfileAboutDto About(
        Input input,
        LearnerProfile learner,
        IReadOnlyList<EducationItemDto> schools,
        IReadOnlyList<WorkItemDto> work,
        QuestAnswerView flex,
        QuestAnswerView arc,
        string city)
    {
        var months = WorkMonths(QuestAnswerView.Parse(input.Answers.GetValueOrDefault(WorkLore)));
        var tags = new List<string>();
        if (learner.ArchetypeName is { } archetype)
        {
            tags.Add(archetype);
        }

        tags.Add(months > 0 ? $"{Duration(months)} experience" : "Fresher");
        if (string.Equals(arc.Str("notice"), "immediate", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("Immediate joiner");
        }

        if (input.Settings.About is { } own)
        {
            return new JobProfileAboutDto(own, false, tags);
        }

        // Muni's summary, written from the quests until the learner rewrites it (FR-13).
        var sentences = new List<string>();
        if (schools.FirstOrDefault() is { } school)
        {
            sentences.Add(school.School.Length > 0 ? $"{school.Title} from {school.School}." : $"{school.Title}.");
        }

        if (work.FirstOrDefault() is { } latest)
        {
            sentences.Add(latest.Employer.Length > 0 ? $"Most recently {latest.Role} at {latest.Employer}." : $"Most recently {latest.Role}.");
        }

        var languages = flex.Items("languages").Select(l => l.Str("name")).Where(n => n.Length > 0).ToList();
        if (languages.Count > 0)
        {
            sentences.Add($"Speaks {JoinAnd(languages)}.");
        }

        var roles = arc.Strings("roles");
        if (roles.Count > 0)
        {
            sentences.Add(city.Length > 0 ? $"Looking for a {roles[0]} role in {city}." : $"Looking for a {roles[0]} role.");
        }

        return new JobProfileAboutDto(string.Join(' ', sentences), true, tags);
    }

    private static IReadOnlyList<QuickWinDto> QuickWins(IReadOnlyList<SkillItemDto> skills, ProfileProgressDto progress)
    {
        var wins = new List<QuickWinDto>();
        foreach (var skill in skills.Where(s => s.Power is null).Take(1))
        {
            wins.Add(new QuickWinDto($"level-{Slug(skill.Name, 40)}", $"Set your level for {skill.Name}", "Better job matches", $"/quests/{Flex}", EditQuery(1)));
        }

        foreach (var quest in progress.Quests)
        {
            foreach (var step in quest.Left)
            {
                wins.Add(new QuickWinDto($"{quest.Slug}-{step.Key}", $"Add your {step.Label.ToLowerInvariant()}", "Completes your Job Profile", $"/quests/{quest.Slug}", EditQuery(step.Part)));
            }
        }

        return wins.Take(3).ToList();
    }

    private static Dictionary<string, int> EditQuery(int part) => new(StringComparer.Ordinal) { ["part"] = part, ["edit"] = 1 };

    private static string StrengthLabel(int percent) => percent switch
    {
        >= 100 => "All-star profile",
        >= 80 => "Strong profile",
        >= 50 => "Good start",
        _ => "Just getting started",
    };

    private static string Location(QuestAnswerView contact, bool withPostal)
    {
        var area = contact.Str("area");
        var city = contact.Str("city");
        var place = string.Join(", ", new[] { area, city }.Where(x => x.Length > 0));
        var postal = contact.Str("postal");
        return withPostal && postal.Length > 0 ? $"{place} {postal}".Trim() : place;
    }

    /// <summary>"3-4.5" → "₹3 – 4.5 LPA" (IN) or "R3 – 4.5k a month" (ZA); anything else as typed.</summary>
    private static string ExpectedPay(string salary, string market)
    {
        if (salary.Length == 0)
        {
            return string.Empty;
        }

        if (SalaryLabels.TryGetValue($"{market}:{salary}", out var label))
        {
            return label;
        }

        var parts = salary.Split('-', 2, StringSplitOptions.TrimEntries);
        var range = parts.Length == 2 ? $"{parts[0]} – {parts[1]}" : salary;
        return market == "ZA" ? $"R{range} / month" : $"₹{range} LPA";
    }

    /// <summary>arc.ts NOTICE: India counts days ("15" → "15 days"); South Africa has its own labels.</summary>
    private static string NoticeLabel(string notice, string market)
    {
        if (notice.Length == 0)
        {
            return string.Empty;
        }

        if (market == "ZA" && ZaNoticeLabels.TryGetValue(notice, out var za))
        {
            return za;
        }

        return int.TryParse(notice, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) ? $"{days} days" : Humanise(notice);
    }

    private static string Period(QuestAnswerView chapter, bool withLength)
    {
        var start = Month(chapter.Str("start"));
        var current = chapter.Bool("current") == true;
        var end = current ? null : Month(chapter.Str("end"));
        if (start is null)
        {
            return string.Empty;
        }

        var text = $"{Format(start.Value)} – {(current || end is null ? "now" : Format(end.Value))}";
        if (!withLength || current || end is null)
        {
            return text;
        }

        var months = ((end.Value.Year - start.Value.Year) * 12) + end.Value.Month - start.Value.Month + 1;
        return months > 0 ? $"{text} · {Duration(months)}" : text;
    }

    private static int WorkMonths(QuestAnswerView lore) =>
        lore.Items("chapters").Sum(c =>
        {
            var start = Month(c.Str("start"));
            var end = c.Bool("current") == true ? null : Month(c.Str("end"));
            if (start is null || end is null)
            {
                return 0;
            }

            return Math.Max(0, ((end.Value.Year - start.Value.Year) * 12) + end.Value.Month - start.Value.Month + 1);
        });

    private static DateOnly? Month(string value) =>
        DateOnly.TryParseExact(value.Length >= 7 ? value[..7] : value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month) ? month : null;

    private static string Format(DateOnly month) => month.ToString("MMM yyyy", CultureInfo.InvariantCulture);

    private static string Duration(int months)
    {
        var (years, rest) = (months / 12, months % 12);
        var y = years == 0 ? string.Empty : years == 1 ? "1 yr" : $"{years} yrs";
        var m = rest == 0 ? string.Empty : rest == 1 ? "1 mo" : $"{rest} mos";
        return string.Join(' ', new[] { y, m }.Where(x => x.Length > 0));
    }

    private static string Initials(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetterOrDigit(w[0])).ToList();
        return words.Count switch
        {
            0 => string.Empty,
            1 => words[0].Length >= 2 ? words[0][..2].ToUpperInvariant() : words[0].ToUpperInvariant(),
            _ => string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0]))),
        };
    }

    private static string Humanise(string value)
    {
        var text = value.Replace('_', ' ').Replace('-', ' ').Trim();
        return Capitalise(text);
    }

    private static string Capitalise(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string JoinAnd(IReadOnlyList<string> items) =>
        items.Count == 1 ? items[0] : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";
}
