using System.Text.Json;
using Learner.Application.Contracts;
using Learner.Application.Events;
using Learner.Application.Handlers;
using Learner.UnitTests.Fakes;

namespace Learner.UnitTests.Services;

public sealed class JobProfileTests
{
    private static readonly byte[] Pdf = "%PDF-1.7\n%"u8.ToArray();

    private readonly LearnerHarness _h = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    private async Task<LearnerRef> SampleLearnerAsync()
    {
        var who = LearnerHarness.NewLearner("Yash");
        await _h.Quests.SavePartAsync(who, "who-dis", 1, LearnerHarness.Part(new { firstName = "Yash", lastName = "Pohare" }), _ct);
        _h.Learner(who).SetVerifiedContact("+919876540697", "yash.pohare@example.com", _h.Clock.GetUtcNow().UtcDateTime);
        await _h.Quests.SavePartAsync(who, "hit-me-up", 1, LearnerHarness.Part(new { postal = "411038", region = "Maharashtra", city = "Pune", area = "Kothrud", linkedin = "linkedin.com/in/yash" }, done: false), _ct);
        await _h.Quests.SavePartAsync(who, "big-brain-stuff", 1, LearnerHarness.Part(new
        {
            level = "graduate", status = "completed", degree = "B.Com", branch = "Accounting & Finance",
            institute = "Savitribai Phule Pune University", year = "2024", scoreKind = "cgpa", score = "7.4",
        }, done: false), _ct);
        await _h.Quests.SavePartAsync(who, "your-flex-era", 1, LearnerHarness.Part(new
        {
            skills = new object[]
            {
                new { id = "customer-service", name = "Customer service", category = "people", level = 3, signature = true },
                new { id = "excel", name = "Microsoft Excel", category = "business", level = 2, signature = false },
                new { id = "tally", name = "Tally basics", category = "business", level = 0, signature = false },
            },
            languages = new[] { new { name = "Hindi", fluency = "native", speak = true, read = true, write = true } },
            trophies = new[] { new { id = "nsdc", name = "Data capturing certificate", issuer = "NSDC", year = "2024", verified = true } },
        }, done: false), _ct);
        await _h.Quests.SavePartAsync(who, "work-lore", 1, LearnerHarness.Part(new
        {
            chapters = new[]
            {
                new { id = "tp", role = "Customer Service Intern", company = "Teleperformance", start = "2025-01", end = "2025-06", current = false, description = "60+ calls a day" },
            },
        }, done: false), _ct);
        await _h.Quests.SavePartAsync(who, "pick-your-arc", 1, LearnerHarness.Part(new
        {
            lookingFor = new[] { "full_time", "internship" },
            roles = new[] { "Customer support executive", "BPO voice" },
            where = new[] { "near", "remote" },
            shifts = new[] { "day", "rotational" },
            notice = "immediate",
            salary = "3-4.5",
        }, done: false), _ct);
        return who;
    }

    [Fact]
    public async Task The_profile_is_built_from_the_quests()
    {
        var who = await SampleLearnerAsync();
        _h.Learner(who).ApplyArchetype("People Pro", "Loves people", _h.Clock.GetUtcNow().UtcDateTime);

        var profile = await _h.JobProfile.GetAsync(who, _ct);

        Assert.Equal(("Yash Pohare", "YP", "Customer support executive · BPO voice"), (profile.Name, profile.Initials, profile.Headline));
        Assert.Equal(new JobArchetypeDto("People Pro", "People"), profile.Archetype);
        Assert.Equal(100, profile.Radar!.People);
        Assert.Equal(new[] { new SignatureSkillDto("Customer service", 3) }, profile.Signature);
        Assert.Equal(new JobProfileCountsDto(3, 1, 1), profile.Counts);
        Assert.Equal(8, profile.ByQuest.Count);

        var work = Assert.Single(profile.Work);
        Assert.Equal(("TE", "Teleperformance", "Pune", "Jan 2025 – Jun 2025 · 6 mos"), (work.Initials, work.Employer, work.City, work.Period));
        var school = Assert.Single(profile.Education);
        Assert.Equal(("B.Com · Accounting & Finance", "2024 · CGPA 7.4", "SP"), (school.Title, school.Detail, school.Initials));
        Assert.Equal(new LanguageItemDto("Hindi", "Speak · Read · Write", "Native"), Assert.Single(profile.Languages));
        Assert.Null(profile.Skills.Single(s => s.Name == "Tally basics").Power);
        var certificate = Assert.Single(profile.Certificates);
        Assert.Equal(("NSDC · 2024", "verified"), (certificate.Issuer, certificate.Status));
        Assert.Equal(1, profile.VerifiedCount);

        Assert.Equal("•••••• 0697", profile.Contact.Mobile);
        Assert.True(profile.Contact.MobileVerified);
        Assert.Equal(("y••••@example.com", "Kothrud, Pune 411038", "Email · WhatsApp"), (profile.Contact.Email, profile.Contact.Location, profile.Contact.InvitesBy));

        Assert.Contains(new LookingForRowDto("Can join", "Immediately", true), profile.LookingFor);
        Assert.Contains(new LookingForRowDto("Expected pay", "₹3 – 4.5 LPA", null, true), profile.LookingFor);
        Assert.Contains(new LookingForRowDto("Where", "Pune + 25 km · open to remote"), profile.LookingFor);

        Assert.True(profile.About.ByMuni);
        Assert.Contains("Teleperformance", profile.About.Text, StringComparison.Ordinal);
        Assert.Equal(new[] { "People Pro", "6 mos experience", "Immediate joiner" }, profile.About.Tags);
        Assert.Equal("Set your level for Tally basics", profile.Strength.QuickWins[0].Title);
        Assert.Equal(new QuestRefDto("pick-your-arc", 1, "Pick Your Arc"), profile.Sources["lookingFor"]);
        Assert.Equal(5, profile.VerifyCode.Length);
        Assert.Empty(profile.Pending);
    }

    [Fact]
    public async Task About_me_can_be_rewritten_and_reset()
    {
        var who = await SampleLearnerAsync();

        var mine = await _h.JobProfile.SaveAboutAsync(who, new SaveAboutRequest("  I love helping people.  "), _ct);
        Assert.Equal(("I love helping people.", false), (mine.About.Text, mine.About.ByMuni));

        var reset = await _h.JobProfile.SaveAboutAsync(who, new SaveAboutRequest(""), _ct);
        Assert.True(reset.About.ByMuni);
    }

    [Fact]
    public async Task Share_settings_default_to_hidden_contact_and_30_days()
    {
        var who = await SampleLearnerAsync();

        var share = await _h.JobProfile.GetShareAsync(who, _ct);
        Assert.StartsWith("https://skillmuni.app/cv/yash-", share.Url, StringComparison.Ordinal);
        Assert.Equal((true, 30, 0), (share.HideContact, share.ExpiresDays, share.ViewsThisWeek));

        var updated = await _h.JobProfile.UpdateShareAsync(who, new UpdateShareRequest(false, 7), _ct);
        Assert.Equal((share.Url, false, 7), (updated.Url, updated.HideContact, updated.ExpiresDays));
    }

    [Fact]
    public async Task Cv_contact_is_the_full_contact()
    {
        var who = await SampleLearnerAsync();
        var contact = await _h.JobProfile.GetCvContactAsync(who, _ct);
        Assert.Equal(new CvContactDto("+91 98765 40697", "yash.pohare@example.com", "Kothrud, Pune", "linkedin.com/in/yash", "₹3 – 4.5 LPA"), contact);
    }

    [Fact]
    public async Task A_newer_cv_suggests_changes_and_only_accepted_ones_are_added()
    {
        var who = await SampleLearnerAsync();
        var prefill = new Dictionary<string, JsonElement>
        {
            ["work-lore"] = JsonSerializer.SerializeToElement(new
            {
                chapters = new[]
                {
                    new { role = "Customer Service Intern", company = "Teleperformance", start = "2025-01", end = "2025-06", current = false },
                    new { role = "Customer Support Executive", company = "Concentrix", start = "2025-07", end = "", current = true },
                },
            }),
            ["your-flex-era"] = JsonSerializer.SerializeToElement(new
            {
                skills = new[] { new { name = "Customer service", level = 3 }, new { name = "Freshdesk", level = 2 } },
                trophies = new[] { new { name = "Customer Service Excellence", issuer = "Udemy" } },
            }),
            ["hit-me-up"] = JsonSerializer.SerializeToElement(new { email = "yash.p@outlook.com" }),
        };
        await new CvParsedHandler(_h.Store, _h.Store, _h.Catalog, _h.Clock)
            .HandleAsync(new CvParsed(Guid.NewGuid(), who.Id, "ok", prefill) { TenantId = LearnerHarness.Tenant }, _ct);

        var changes = (await _h.JobProfile.UploadCvAsync(who, new UploadedFile("cv.pdf", "application/pdf", Pdf.Length, new MemoryStream(Pdf)), _ct)).Value;

        Assert.Equal(new[] { "job", "skill", "certificate", "contact" }, changes.Select(c => c.Kind));
        Assert.Equal(new CvChangeDto("job-concentrix-customer-support-executive", "job", "Customer Support Executive · Concentrix", "New job · Jul 2025 – now", "add"), changes[0]);
        Assert.Equal("keep", changes[3].Suggest);
        Assert.Single(_h.Blobs.Blobs);

        var profile = await _h.JobProfile.ApplyChangesAsync(who, new ApplyCvChangesRequest(changes.Select(c => c.Id).ToList()), _ct);

        Assert.Equal(2, profile.Work.Count);
        Assert.Contains(profile.Skills, s => s.Name == "Freshdesk");
        Assert.Contains(profile.Certificates, c => c.Title == "Customer Service Excellence");
        Assert.Equal("y••••@example.com", profile.Contact.Email);

        var again = (await _h.JobProfile.UploadCvAsync(who, new UploadedFile("cv.pdf", "application/pdf", Pdf.Length, new MemoryStream(Pdf)), _ct)).Value;
        Assert.Equal("contact", Assert.Single(again).Kind);
        Assert.Single(_h.Blobs.Blobs);
    }

    [Fact]
    public async Task Only_pdf_or_word_cvs_up_to_5_mb_are_accepted()
    {
        var who = await SampleLearnerAsync();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        Assert.Equal("unsupported-type", (await _h.JobProfile.UploadCvAsync(who, new UploadedFile("cv.png", "image/png", png.Length, new MemoryStream(png)), _ct)).Error!.Code);
        Assert.Equal("file-missing", (await _h.JobProfile.UploadCvAsync(who, null, _ct)).Error!.Code);
        Assert.Equal("too-large", (await _h.JobProfile.UploadCvAsync(who, new UploadedFile("cv.pdf", "application/pdf", 6 * 1024 * 1024, new MemoryStream(Pdf)), _ct)).Error!.Code);

        byte[] docx = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00];
        Assert.True((await _h.JobProfile.UploadCvAsync(who, new UploadedFile("cv.docx", "application/octet-stream", docx.Length, new MemoryStream(docx)), _ct)).IsSuccess);
        Assert.Empty(_h.Store.EventsOf<JobProfileCompleted>());
    }

    [Fact]
    public void Validators_check_about_share_and_apply()
    {
        Assert.False(new SaveAboutRequestValidator().Validate(new SaveAboutRequest(new string('a', 401))).IsValid);
        Assert.True(new SaveAboutRequestValidator().Validate(new SaveAboutRequest("Hi")).IsValid);
        var share = new UpdateShareRequestValidator(Microsoft.Extensions.Options.Options.Create(_h.Settings));
        Assert.True(share.Validate(new UpdateShareRequest(true, 90)).IsValid);
        Assert.False(share.Validate(new UpdateShareRequest(true, 45)).IsValid);
        Assert.False(new ApplyCvChangesRequestValidator().Validate(new ApplyCvChangesRequest(null)).IsValid);
    }
}
