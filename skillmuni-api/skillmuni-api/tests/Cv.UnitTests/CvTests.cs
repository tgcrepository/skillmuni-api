using System.Text.Json;
using Cv.Application;
using Cv.Application.Parsing;
using Cv.Application.Services;
using Cv.Domain;

namespace Cv.UnitTests;

public sealed class InterpreterTests
{
    private static CvExtract Read(string market, params string[] lines) =>
        new CvInterpreter().Interpret(new CvText(lines, "en", 0.95, 1, false), market);

    [Fact]
    public void Reads_an_indian_cv()
    {
        var e = Read(
            "IN",
            "ASHA RAO",
            "Pune, Maharashtra 411038 | +91 98765 40697 | Asha.Rao@Example.com",
            "Education",
            "B.Com, Savitribai Phule Pune University, 2024",
            "Experience",
            "Customer Support Intern at Teleperformance, Jan 2024 - Jun 2024",
            "Skills: Customer service, MS Excel, Typing, Syspro",
            "Languages: Hindi (native), English (fluent), Marathi");

        Assert.Equal(new PersonName("Asha", "Rao"), e.Name);
        Assert.Equal(new CvContact("asha.rao@example.com", "+919876540697", "Pune", "Maharashtra", "411038"), e.Contact);
        var degree = Assert.Single(e.Education);
        Assert.Equal(("graduate", "2024"), (degree.Level, degree.Year));
        Assert.Contains("University", degree.Institute, StringComparison.Ordinal);
        var job = Assert.Single(e.Work);
        Assert.Equal(("Customer Support Intern", "Teleperformance", "2024-01", "2024-06", "internship"), (job.Role, job.Company, job.Start, job.End, job.Type));
        Assert.Contains(e.Skills, s => s.Id == "customer-service");
        Assert.Contains(e.Skills, s => s.Id == "excel");
        Assert.Contains(e.Skills, s => s is { Category: "custom", Name: "Syspro" });
        Assert.Equal(new[] { "Hindi", "English", "Marathi" }, e.Languages.Select(l => l.Name));
        Assert.True(e.Languages[0].Native);
        Assert.Equal(string.Empty, e.Languages[2].Fluency);
    }

    [Fact]
    public void Reads_a_south_african_cv_and_maps_language_spellings()
    {
        var e = Read(
            "ZA",
            "Nkosazana Dlamini",
            "Randburg, Gauteng | 071 234 5678",
            "Education",
            "Matric (NSC), Randburg High School, 2021",
            "Work experience",
            "Cashier, Shoprite, Mar 2022 - Dec 2023",
            "Languages: Zulu (home language), English, Sotho");

        Assert.Equal("+27712345678", e.Contact.Phone);
        Assert.Equal(("Randburg", "Gauteng"), (e.Contact.City, e.Contact.Region));
        Assert.Equal("class12", Assert.Single(e.Education).Level);
        var job = Assert.Single(e.Work);
        Assert.Equal(("full_time", true), (job.Type, job.TypeUnsure));
        Assert.Equal(new[] { "isiZulu", "English", "Sesotho" }, e.Languages.Select(l => l.Name));
    }

    [Fact]
    public void Private_details_are_noted_as_skipped_and_never_kept()
    {
        var e = Read("IN", "Ravi Kumar", "Aadhaar: 2345 6789 0123", "PAN ABCDE1234F", "Date of birth: 01/01/2003", "Marital status: Single", "+91 98765 40697");

        Assert.Equal(new[] { "ID number", "marital status", "date of birth" }, e.Skipped);
        var json = JsonSerializer.Serialize(e);
        Assert.DoesNotContain("2345 6789 0123", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ABCDE1234F", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_phone_number_is_not_mistaken_for_an_id()
    {
        var e = Read("IN", "Ravi Kumar", "+919876540697");
        Assert.DoesNotContain("ID number", e.Skipped);
    }

    [Fact]
    public void Current_jobs_have_no_end()
    {
        var job = Assert.Single(Read("IN", "Experience", "Sales Associate at Reliance Retail, 2023 - Present").Work);
        Assert.True(job.Current);
        Assert.Null(job.End);
        Assert.Equal("2023-01", job.Start);
    }
}

public sealed class SummaryAndPrefillTests
{
    private static readonly CvExtract Extract = new(
        new PersonName("Nkosazana", "Dlamini"),
        new CvContact("nkosazanadlamini@gmail.com", "+27712345678", "Randburg", "Gauteng", null),
        [new CvLanguage("isiZulu", "native", true), new CvLanguage("English", "fluent", false), new CvLanguage("Sesotho", string.Empty, false)],
        [new CvEducation("class12", "Matric (NSC)", "Randburg High School", "2021"), new CvEducation("certificate", "Contact Centre Operations NQF 4", null, null)],
        [new CvJob("Call Centre Agent", "Teleperformance", "2024-01", "2024-06", false, "internship", false), new CvJob("Cashier", "Shoprite", "2022-03", "2023-12", false, "part_time", false)],
        [new CvSkill("customer-service", "Customer service", "people", 2), new CvSkill("custom-syspro", "Syspro", "custom", 0)],
        ["ID number"],
        "en");

    [Fact]
    public void Summary_matches_the_screen_copy()
    {
        var summary = CvSummaryBuilder.Build(Extract, "ZA", new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(5, summary.QuestsPrefilled);
        Assert.Equal(1, summary.SkillsMatched);
        Assert.Equal(3, summary.ToCheck);
        var contact = summary.Sections.Single(s => s.Quest == 2);
        Assert.Equal("+27 71 234 5678", contact.Fields.Single(f => f.Key == "mobile").Value);
        Assert.Equal("Found a city only — add your postal code", contact.Fields.Single(f => f.Key == "area").Check);
        Assert.Equal("4 found · 1 to check", contact.Badge);
        var work = summary.Sections.Single(s => s.Quest == 5);
        Assert.Equal("2 chapters · 2 yrs 4 mos", work.Badge);
        Assert.Equal("Call Centre Agent (internship) · Teleperformance · Jan 2024 – Jun 2024", work.Fields[0].Value);
        Assert.Equal("Jul 2024 – now · shown only to you", work.Fields.Single(f => f.Key == "gap").Value);
        Assert.Equal("How well do you speak Sesotho?", summary.Sections.Single(s => s.Quest == 4).Fields.Single(f => f.Key == "languages").Check);
        Assert.Equal(new[] { "ID number" }, summary.Skipped);
    }

    [Fact]
    public void Prefill_uses_the_quest_screens_shapes()
    {
        var prefill = QuestPrefill.Build(Extract, includeEmail: false);

        Assert.Equal("Dlamini", prefill["who-dis"].GetProperty("lastName").GetString());
        Assert.Equal("712345678", prefill["hit-me-up"].GetProperty("phone").GetString());
        Assert.False(prefill["hit-me-up"].TryGetProperty("email", out _));
        Assert.Equal("class12", prefill["big-brain-stuff"].GetProperty("level").GetString());
        var skill = prefill["your-flex-era"].GetProperty("skills")[0];
        Assert.Equal(("customer-service", "cv"), (skill.GetProperty("id").GetString(), skill.GetProperty("source").GetString()));
        var chapter = prefill["work-lore"].GetProperty("chapters")[0];
        Assert.Equal(("internship", "2024-01", true), (chapter.GetProperty("type").GetString(), chapter.GetProperty("start").GetString(), chapter.GetProperty("fromCv").GetBoolean()));
    }
}

public sealed class FileTests
{
    [Theory]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, "application/pdf")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }, null)]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, null)]
    public void Types_come_from_the_bytes(byte[] head, string? type) => Assert.Equal(type, CvFiles.Detect(head, head));

    [Fact]
    public void A_docx_is_a_zip_with_a_word_document()
    {
        var docx = new byte[] { 0x50, 0x4B, 0x03, 0x04 }.Concat("....word/document.xml...."u8.ToArray()).ToArray();
        var zip = new byte[] { 0x50, 0x4B, 0x03, 0x04 }.Concat("....photos/1.jpg...."u8.ToArray()).ToArray();
        Assert.Equal(CvFiles.Docx, CvFiles.Detect(docx, docx));
        Assert.Null(CvFiles.Detect(zip, zip));
    }

    [Fact]
    public void An_encrypted_pdf_is_recognised() =>
        Assert.True(CvFiles.IsEncryptedPdf(CvHarness.Pdf("trailer << /Encrypt 12 0 R >>")));
}

public sealed class CvServiceTests
{
    private readonly CvHarness _h = new();
    private readonly CancellationToken _ct = CancellationToken.None;

    [Fact]
    public async Task Upload_stores_the_file_queues_the_job_and_wakes_the_worker()
    {
        var learner = Guid.NewGuid();
        var result = await _h.Service.UploadAsync(learner, "IN", [CvHarness.File(CvHarness.Pdf(), "C:\\docs\\my cv.pdf")], _ct);

        var document = Assert.Single(_h.Store.Documents);
        Assert.Equal(document.Id.ToString(), result.Value.CvId);
        Assert.Equal(("my cv.pdf", ParseState.Queued, "application/pdf"), (document.FileName, document.State, document.ContentType));
        Assert.Single(_h.Blobs.Blobs);
        Assert.Equal(1, _h.Signal.Count);
        Assert.Equal("parsing", (await _h.Service.StatusAsync(learner, document.Id, _ct)).Value.State);
    }

    [Fact]
    public async Task Refused_uploads_store_nothing()
    {
        var learner = Guid.NewGuid();
        var word97 = new UploadedFile("old.doc", "application/msword", 8, new MemoryStream([0xD0, 0xCF, 0x11, 0xE0, 0, 0, 0, 0]));

        Assert.Equal("unsupported-type", (await _h.Service.UploadAsync(learner, "IN", [word97], _ct)).Error!.Code);
        Assert.Equal("file-missing", (await _h.Service.UploadAsync(learner, "IN", [], _ct)).Error!.Code);
        _h.Settings.MaxBytes = 4;
        Assert.Equal("too-large", (await _h.Service.UploadAsync(learner, "IN", [CvHarness.File(CvHarness.Pdf())], _ct)).Error!.Code);
        Assert.Empty(_h.Blobs.Blobs);
        Assert.Empty(_h.Store.Documents);
    }

    [Fact]
    public async Task Photographed_pages_must_all_be_photos()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };
        var pages = new[] { new UploadedFile("p1.jpg", "image/jpeg", jpeg.Length, new MemoryStream(jpeg)), CvHarness.File(CvHarness.Pdf()) };
        Assert.Equal("unsupported-type", (await _h.Service.UploadAsync(Guid.NewGuid(), "IN", pages, _ct)).Error!.Code);
    }

    [Fact]
    public async Task Uploads_are_capped_per_day()
    {
        var learner = Guid.NewGuid();
        _h.Settings.UploadsPerDay = 2;
        for (var i = 0; i < 2; i++)
        {
            await _h.Service.UploadAsync(learner, "IN", [CvHarness.File(CvHarness.Pdf())], _ct);
        }

        Assert.Equal("too-many-uploads", (await _h.Service.UploadAsync(learner, "IN", [CvHarness.File(CvHarness.Pdf())], _ct)).Error!.Code);
        _h.Clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1));
        Assert.True((await _h.Service.UploadAsync(learner, "IN", [CvHarness.File(CvHarness.Pdf())], _ct)).IsSuccess);
    }

    [Fact]
    public async Task Someone_elses_cv_is_not_found()
    {
        var (_, cvId) = await _h.UploadAndParseAsync("cv.pdf");
        Assert.Equal("cv-not-found", (await _h.Service.SummaryAsync(Guid.NewGuid(), cvId, _ct)).Error!.Code);
    }

    [Fact]
    public async Task A_clean_parse_finishes_summarises_and_publishes_the_prefill()
    {
        var (learner, cvId) = await _h.UploadAndParseAsync("asha.pdf");

        var status = (await _h.Service.StatusAsync(learner, cvId, _ct)).Value;
        Assert.Equal(("done", 100, (string?)null), (status.State, status.Percent, status.Outcome));
        var summary = (await _h.Service.SummaryAsync(learner, cvId, _ct)).Value;
        Assert.Equal(5, summary.QuestsPrefilled);
        Assert.Contains("date of birth", summary.Skipped);
        var parsed = Assert.Single(_h.Store.Events.OfType<CvParseCompleted>());
        Assert.Equal(learner, parsed.LearnerId);
        Assert.Equal("asha.rao@example.com", parsed.Prefill["hit-me-up"].GetProperty("email").GetString());
    }

    [Theory]
    [InlineData("stuck.pdf", "failed", "parse-failed")]
    [InlineData("afrikaans.pdf", "done", "other-language")]
    [InlineData("nowork.pdf", "done", "no-work-history")]
    [InlineData("little.pdf", "done", "little-found")]
    [InlineData("taken.pdf", "done", "email-taken")]
    public async Task Outcomes_match_the_figma_states(string fileName, string state, string outcome)
    {
        var (learner, cvId) = await _h.UploadAndParseAsync(fileName);
        var status = (await _h.Service.StatusAsync(learner, cvId, _ct)).Value;
        Assert.Equal((state, outcome), (status.State, status.Outcome));
    }

    [Fact]
    public async Task A_taken_email_is_left_out_of_the_prefill()
    {
        await _h.UploadAndParseAsync("taken.pdf");
        var parsed = Assert.Single(_h.Store.Events.OfType<CvParseCompleted>());
        Assert.False(parsed.Prefill["hit-me-up"].TryGetProperty("email", out _));
    }

    [Fact]
    public async Task A_locked_pdf_fails_before_reading()
    {
        var (learner, cvId) = await _h.UploadAndParseAsync("cv.pdf", CvHarness.Pdf("trailer << /Encrypt 5 0 R >>"));
        Assert.Equal("locked-pdf", (await _h.Service.StatusAsync(learner, cvId, _ct)).Value.Outcome);
    }

    [Fact]
    public async Task A_blurry_photo_is_s4()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };
        var (learner, cvId) = await _h.UploadAndParseAsync("blurry.jpg", jpeg);
        Assert.Equal(("failed", "blurry-photo"), ((await _h.Service.StatusAsync(learner, cvId, _ct)).Value.State, (await _h.Service.StatusAsync(learner, cvId, _ct)).Value.Outcome));
    }

    [Fact]
    public async Task Delete_removes_the_file_and_the_extract()
    {
        var (learner, cvId) = await _h.UploadAndParseAsync("asha.pdf");

        Assert.True((await _h.Service.DeleteAsync(learner, cvId, _ct)).IsSuccess);
        Assert.Empty(_h.Store.Documents);
        Assert.Empty(_h.Blobs.Blobs);
        Assert.Single(_h.Store.Events.OfType<CvDeleted>());
    }

    [Fact]
    public async Task Erasure_removes_every_cv_of_the_learner()
    {
        var (learner, _) = await _h.UploadAndParseAsync("asha.pdf");
        var handler = new AccountErasedHandler(_h.Store, _h.Blobs, Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountErasedHandler>.Instance);

        await handler.HandleAsync(new AccountErased(learner) { TenantId = "skillmuni-in" }, _ct);

        Assert.Empty(_h.Store.Documents);
        Assert.Empty(_h.Blobs.Blobs);
    }
}
