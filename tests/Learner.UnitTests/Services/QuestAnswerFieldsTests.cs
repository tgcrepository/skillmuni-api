using Learner.Application.Contracts;
using Learner.Application.Events;
using Learner.Application.Services;
using Learner.Domain;
using Learner.UnitTests.Fakes;

namespace Learner.UnitTests.Services;

/// <summary>
/// The Job Profile, the Careers profile and the Profile read the quest answers with the field names the quest screens
/// save (features/quests/parts/*.ts, data/*.ts). Every learner here fills the quests exactly like the app does.
/// </summary>
public sealed class QuestAnswerFieldsTests
{
    private readonly LearnerHarness _h = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private static readonly object[] Deck =
    [
        new { id = "customer-service", name = "Customer service", category = "people", level = 3, source = "suggested" },
        new { id = "tally", name = "Tally", category = "business", level = 0, source = "custom" },
        new { id = "microsoft-excel", name = "Microsoft Excel", category = "business", level = 3, source = "search", esco = "http://data.europa.eu/esco/skill/x" },
        new { id = "verbal-communication", name = "Verbal communication", category = "people", level = 2, source = "suggested" },
        new { id = "data-entry", name = "Data entry", category = "data", level = 2, source = "suggested" },
    ];

    private static readonly object[] Trophies =
    [
        new
        {
            id = "t1", name = "Data capturing certificate", issuer = "NSDC · Skill India", issuedOn = "2024-03", expiresOn = "", noExpiry = true,
            credentialUrl = "", credentialId = "SI-1", fileName = "nsdc.pdf", verified = false, linkedSkills = new[] { "data-entry" }, source = "manual",
        },
        new
        {
            id = "t2", name = "NPTEL Programming in Java", issuer = "NPTEL", issuedOn = "2023-10", expiresOn = "", noExpiry = true,
            credentialUrl = "", credentialId = "N-1", fileName = "", verified = true, linkedSkills = new[] { "microsoft-excel" }, source = "digilocker",
        },
    ];

    private static readonly object[] Chapters =
    [
        new
        {
            id = "c1", type = "internship", role = "Customer Service Intern", company = "Teleperformance", start = "2025-01", end = "2025-06",
            current = false, mode = "onsite", skills = new[] { "customer-service" }, description = "60+ calls a day",
        },
        new
        {
            id = "c2", type = "part_time", role = "Sales Associate", company = "Reliance Trends", start = "2023-03", end = "2024-04",
            current = false, mode = "onsite", skills = new[] { "verbal-communication" }, description = "",
        },
    ];

    private async Task<LearnerRef> FullLearnerAsync(bool invites = true)
    {
        var who = LearnerHarness.NewLearner("Priya");
        Task Save(string slug, int part, object values) => _h.Quests.SavePartAsync(who, slug, part, LearnerHarness.Part(values), _ct);

        await Save("who-dis", 1, new { firstName = "Priya", lastName = "Sharma", nickname = "" });
        await Save("who-dis", 2, new { idType = "aadhaar", idNumber = "", birthday = "21 / 08 / 2004", idMasked = "XXXX XXXX 4821", idLast4 = "4821", idVia = "digilocker" });
        await Save("who-dis", 3, new { gender = "female", homeLanguage = "Hindi", disability = "no" });
        await _h.Quests.CompleteAsync(who, "who-dis", _ct);
        await Save("hit-me-up", 1, new { phone = "9876540697", phoneVerified = true, email = "priya@example.com", whatsapp = true });
        await Save("hit-me-up", 2, new { relation = "parent", relationText = "", backupName = "Sunita", backupPhone = "9812345670", altEmail = "" });
        await Save("hit-me-up", 3, new { postal = "400001", region = "Maharashtra", district = "Mumbai", city = "Mumbai", area = "Fort", line1 = "12 Road", unit = "", landmark = "" });
        await _h.Quests.CompleteAsync(who, "hit-me-up", _ct);
        await Save("big-brain-stuff", 1, new { level = "graduate", status = "completed" });
        await Save("big-brain-stuff", 2, new { degree = "B.Com", branch = "Accounting & Finance", institute = "University of Mumbai", board = "", year = "2024", studyYear = "", scoreKind = "cgpa", score = "7.8" });
        await Save("big-brain-stuff", 3, new
        {
            timeline = new object[]
            {
                new { id = "tl1", level = "12th", board = "Maharashtra State Board", stream = "Commerce", name = "", institute = "Jai Hind College", year = "2021", score = "78" },
                new { id = "tl2", level = "course", board = "", stream = "", name = "Tally Prime with GST", institute = "NIIT", year = "2023", score = "" },
                new { id = "tl3", level = "education", board = "", stream = "", name = "", institute = "IGNOU", year = "2025", score = "", qual = "diploma", degree = "", branch = "Retail" },
            },
        });
        await _h.Quests.CompleteAsync(who, "big-brain-stuff", _ct);
        await Save("your-flex-era", 1, new { skills = Deck });
        await Save("your-flex-era", 2, new { languages = new[] { new { name = "Hindi", fluency = "native", speak = true, read = true, write = false, motherTongue = true, fromCv = false } } });
        await Save("your-flex-era", 3, new { trophies = Trophies, skills = Deck });
        await _h.Quests.CompleteAsync(who, "your-flex-era", _ct);
        await Save("work-lore", 1, new { fresher = false, workTypes = new[] { "internship", "part_time" }, starters = Array.Empty<string>() });
        await Save("work-lore", 2, new { chapters = Chapters, chapterDraft = (object?)null, chapterDrafts = (object?)null });
        await Save("work-lore", 3, new { chapters = Chapters, gapReasons = new { } });
        await _h.Quests.CompleteAsync(who, "work-lore", _ct);
        await Save("pick-your-arc", 1, new
        {
            lookingFor = new[] { "full_time", "apprenticeship" },
            roles = new[] { "Customer support executive" },
            where = new[] { "near", "region", "relocate" },
            shifts = new[] { "day", "night" },
            notice = "30",
            salary = "10+",
        });
        await _h.Quests.CompleteAsync(who, "pick-your-arc", _ct);
        await Save("drop-the-receipts", 1, new
        {
            receipts = new Dictionary<string, object>
            {
                ["edu"] = new { status = "checking", fileName = "bcom.pdf", at = "15 Oct 2026" },
                ["tl-tl1"] = new { status = "verified", fileName = "hsc.pdf", at = "15 Oct 2026" },
                ["tr-t1"] = new { status = "checking", fileName = "nsdc.pdf", at = "15 Oct 2026" },
                ["ch-c1"] = new { status = "verified", fileName = "letter.pdf", at = "15 Oct 2026" },
            },
        });
        await _h.Quests.CompleteAsync(who, "drop-the-receipts", _ct);
        await Save("vibe-check", 1, new { discoverable = true, invites });
        await _h.Quests.CompleteAsync(who, "vibe-check", _ct);
        return who;
    }

    [Fact]
    public async Task A_learner_who_filled_every_quest_has_a_complete_job_profile()
    {
        var who = await FullLearnerAsync();

        var profile = await _h.JobProfile.GetAsync(who, _ct);

        Assert.Equal("Priya Sharma", profile.Name);
        Assert.Equal(100, profile.Strength.Percent);
        Assert.All(profile.ByQuest, q => Assert.Empty(q.Left));
        Assert.Single(_h.Store.EventsOf<JobProfileCompleted>());

        // The first three skills with a level, in deck order (level 0 = not set yet).
        Assert.Equal(new[] { "Customer service", "Microsoft Excel", "Verbal communication" }, profile.Signature.Select(s => s.Name));
        Assert.Equal(new[] { "Customer service", "Microsoft Excel", "Verbal communication" }, profile.Skills.Where(s => s.Signature).Select(s => s.Name));
        Assert.Null(profile.Skills.Single(s => s.Name == "Tally").Power);

        Assert.Equal(
            new[] { "B.Com · Accounting & Finance", "12th · Commerce", "Tally Prime with GST", "Diploma · Retail" },
            profile.Education.Select(e => e.Title));
        Assert.Equal(new[] { "checking", "verified", "none", "none" }, profile.Education.Select(e => e.Proof));
        Assert.Equal(("Maharashtra State Board", "2021 · 78%"), (profile.Education[1].School, profile.Education[1].Detail));

        Assert.Equal(new[] { "verified", "none" }, profile.Work.Select(w => w.Proof));
        Assert.All(profile.Work, w => Assert.Equal("Mumbai", w.City));
        Assert.Equal(new[] { ("checking", "48 h"), ("verified", (string?)null) }, profile.Certificates.Select(c => (c.Status, c.Eta)));
        Assert.Equal("NSDC · Skill India · 2024", profile.Certificates[0].Issuer);
        Assert.Equal(3, profile.VerifiedCount);

        Assert.Equal(("Email · WhatsApp", true), (profile.Contact.InvitesBy, profile.Contact.Discoverable));
        var rows = profile.LookingFor.ToDictionary(r => r.Label, r => r.Value);
        Assert.Equal("Full-time · Apprenticeship", rows["Looking for"]);
        Assert.Equal("Mumbai + 25 km · Anywhere in Maharashtra · open to relocating", rows["Where"]);
        Assert.Equal("Day · Night (US/UK shift)", rows["Shifts"]);
        Assert.Equal("30 days", rows["Can join"]);
        Assert.Equal("₹10 LPA +", rows["Expected pay"]);
        Assert.Contains("B.Com · Accounting & Finance from University of Mumbai.", profile.About.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invites_off_and_skipped_steps_use_the_quest_screens_keys()
    {
        var who = LearnerHarness.NewLearner("Ravi");
        await _h.FinishQuestsAsync(who, 1);
        await _h.Quests.SavePartAsync(who, "hit-me-up", 1, LearnerHarness.Part(new { phone = "9876540697", whatsapp = true }), _ct);
        await _h.Quests.SavePartAsync(who, "hit-me-up", 2, LearnerHarness.Part(new { backupSkipped = true }, xp: false), _ct);
        await _h.Quests.SavePartAsync(who, "vibe-check", 1, LearnerHarness.Part(new { discoverable = false, invites = false }, done: false), _ct);

        var profile = await _h.JobProfile.GetAsync(who, _ct);

        Assert.Equal((JobProfileBuilder.InvitesOff, false), (profile.Contact.InvitesBy, profile.Contact.Discoverable));
        Assert.Equal(new[] { "name", "id", "tea" }, _h.Catalog.Find("who-dis")!.Parts.Select(p => p.Key));
        Assert.Equal(new[] { "digits", "backup", "spot" }, _h.Catalog.Find("hit-me-up")!.Parts.Select(p => p.Key));
        Assert.Equal(new[] { "deck", "passport", "trophies" }, _h.Catalog.Find("your-flex-era")!.Parts.Select(p => p.Key));
        Assert.Equal(new[] { "types", "chapter", "timeline" }, _h.Catalog.Find("work-lore")!.Parts.Select(p => p.Key));
        Assert.Equal("vibe", Assert.Single(_h.Catalog.Find("vibe-check")!.Parts).Key);
        var q2 = profile.ByQuest.Single(q => q.Slug == "hit-me-up");
        Assert.Equal(new[] { "backup", "spot" }, q2.Left.Select(l => l.Key));
        var q3 = profile.ByQuest.Single(q => q.Slug == "big-brain-stuff");
        Assert.Equal("Timeline", q3.Left.Single(l => l.Key == "timeline").Label);
    }

    [Fact]
    public async Task Careers_sees_skills_proven_by_verified_trophies_and_work_receipts()
    {
        var who = await FullLearnerAsync();

        var careers = (await new CareersProfileService(_h.Store, _h.Store).GetAsync(who.Id, _ct)).Value;

        Assert.Equal(("Priya Sharma", "Mumbai", "Maharashtra", true), (careers.Name, careers.City, careers.State, careers.Relocate));
        var evidence = careers.Skills.ToDictionary(s => s.Name, s => s.Evidence);

        // t2 is a DigiLocker trophy linked to Excel; c1's work receipt is verified and used Customer service.
        Assert.Equal("verified", evidence["Microsoft Excel"]);
        Assert.Equal("verified", evidence["Customer service"]);

        // t1's receipt is still being checked; c2 has no receipt.
        Assert.Equal("declared", evidence["Data entry"]);
        Assert.Equal("declared", evidence["Verbal communication"]);
    }

    [Fact]
    public async Task Quest_1_fills_the_profile_gender_and_birthday_once()
    {
        var who = await FullLearnerAsync();
        var learner = _h.Learner(who);
        Assert.Equal((Gender.Female, new DateOnly(2004, 8, 21)), (learner.Gender, learner.DateOfBirth));

        await _h.Profile.SaveAsync(who, new ProfileEditRequest([], "male", "2003-01-02"), _ct);
        await _h.Quests.SavePartAsync(who, "who-dis", 3, LearnerHarness.Part(new { gender = "female" }), _ct);

        Assert.Equal((Gender.Male, new DateOnly(2003, 1, 2)), (learner.Gender, learner.DateOfBirth));
    }
}
