using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;

namespace Learner.IntegrationTests;

[Collection(LearnerApiCollection.Name)]
public sealed class LearnerEndpointsTests(LearnerApiFactory api)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];

    [Fact]
    public async Task No_session_is_401()
    {
        var response = await api.CreateClient().GetAsync("/api/v1/job-profile");
        Assert.Equal("no-session", await response.ProblemCodeAsync(HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task A_token_from_another_tenant_is_403()
    {
        var client = api.CreateLearnerClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "skillmuni-za");
        Assert.Equal("tenant-mismatch", await (await client.GetAsync("/api/v1/job-profile")).ProblemCodeAsync(HttpStatusCode.Forbidden));
    }

    [Fact]
    public async Task Progress_is_no_longer_served_here()
    {
        // BR-138: GET me/progress is Gamification's (the gateway routes it there).
        Assert.Equal(HttpStatusCode.NotFound, (await api.CreateLearnerClient().GetAsync("/api/v1/me/progress")).StatusCode);
    }

    [Fact]
    public async Task A_new_learners_job_profile_is_empty()
    {
        var profile = await (await api.CreateLearnerClient().GetAsync("/api/v1/job-profile")).JsonAsync(HttpStatusCode.OK);

        Assert.Equal(8, profile.GetProperty("byQuest").GetArrayLength());
        Assert.Equal(0, profile.GetProperty("strength").GetProperty("percent").GetInt32());
        Assert.Equal(0, profile.GetProperty("work").GetArrayLength());
        Assert.True(profile.GetProperty("about").GetProperty("byMuni").GetBoolean());
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("archetype").ValueKind);
        Assert.Equal("Drop the Receipts", profile.GetProperty("sources").GetProperty("certificates").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Job_profile_about_share_and_newer_cv()
    {
        var client = api.CreateLearnerClient(out var id);
        await client.PutJsonAsync("/api/v1/me/quests/work-lore/parts/1", new { values = new { chapters = new[] { new { role = "Cashier", company = "Shoprite", start = "2022-03", end = "2023-12", current = false } } }, done = false });

        var about = await (await client.PutJsonAsync("/api/v1/job-profile/about", new { text = "I love helping people." })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("I love helping people.", about.GetProperty("about").GetProperty("text").GetString());
        Assert.Equal("Mar 2022 – Dec 2023 · 1 yr 10 mos", about.GetProperty("work")[0].GetProperty("period").GetString());
        Assert.Equal("validation", await (await client.PutJsonAsync("/api/v1/job-profile/about", new { text = new string('a', 401) })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

        var share = await (await client.GetAsync("/api/v1/job-profile/share")).JsonAsync(HttpStatusCode.OK);
        Assert.True(share.GetProperty("hideContact").GetBoolean());
        Assert.Equal(30, share.GetProperty("expiresDays").GetInt32());
        var updated = await (await client.PutJsonAsync("/api/v1/job-profile/share", new { hideContact = false, expiresDays = 90 })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal((share.GetProperty("url").GetString(), 90), (updated.GetProperty("url").GetString(), updated.GetProperty("expiresDays").GetInt32()));
        Assert.Equal("validation", await (await client.PutJsonAsync("/api/v1/job-profile/share", new { hideContact = true, expiresDays = 45 })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

        var contact = await (await client.GetAsync("/api/v1/job-profile/cv-contact")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, contact.GetProperty("linkedin").ValueKind);

        var prefill = new Dictionary<string, object>
        {
            ["your-flex-era"] = new { skills = new[] { new { name = "Freshdesk", level = 2 } } },
        };
        await api.DeliverAsync("cv.parse.completed", Guid.NewGuid(), new { cvId = Guid.NewGuid(), learnerId = id, outcome = "ok", prefill, tenantId = LearnerApiFactory.Tenant });

        using var form = new MultipartFormDataContent { { new ByteArrayContent("%PDF-1.7\n"u8.ToArray()), "cv", "cv.pdf" } };
        var changes = await (await client.PostAsync("/api/v1/job-profile/cv", form)).JsonAsync(HttpStatusCode.OK);
        var change = changes.EnumerateArray().Single(c => c.GetProperty("kind").GetString() == "skill");
        Assert.Equal(("skill-freshdesk", "add"), (change.GetProperty("id").GetString(), change.GetProperty("suggest").GetString()));

        var applied = await (await client.PostJsonAsync("/api/v1/job-profile/cv/apply", new { ids = new[] { "skill-freshdesk" } })).JsonAsync(HttpStatusCode.OK);
        Assert.Contains(applied.GetProperty("skills").EnumerateArray(), s => s.GetProperty("name").GetString() == "Freshdesk");
        Assert.Equal("validation", await (await client.PostJsonAsync("/api/v1/job-profile/cv/apply", new { })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

        using var bad = new MultipartFormDataContent { { new ByteArrayContent(Png), "cv", "cv.png" } };
        Assert.Equal("unsupported-type", await (await client.PostAsync("/api/v1/job-profile/cv", bad)).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task A_confirmed_claim_publishes_the_step_and_quest_events_for_quests_1_to_5()
    {
        var client = api.CreateLearnerClient(out var id);
        await client.GetAsync("/api/v1/job-profile");

        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("identity.claim.confirmed", Guid.NewGuid(), new { claimId = Guid.NewGuid(), accountId = id, tenantId = LearnerApiFactory.Tenant }));

        var outbox = await api.QueryAsync(db => db.OutboxMessages.AsNoTracking().Select(m => new { m.EventType, m.Payload }).ToListAsync());
        var events = outbox.Where(m => m.Payload.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase)).Select(m => m.EventType).ToList();
        Assert.Equal(15, events.Count(e => e == "learner.quest.step-completed"));
        Assert.Equal(5, events.Count(e => e == "learner.quest.completed"));
        var learner = await api.QueryAsync(db => db.Learners.AsNoTracking().SingleAsync(l => l.Id == id));
        Assert.Equal(5, learner.QuestsCompleted);
    }

    [Fact]
    public async Task Quest_parts_save_merge_and_complete()
    {
        var client = api.CreateLearnerClient();

        var save = await client.PutJsonAsync("/api/v1/me/quests/who-dis/parts/1", new { values = new { firstName = "Asha", lastName = "Rao" }, done = true });
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        await client.PutJsonAsync("/api/v1/me/quests/who-dis/parts/3", new { values = new { birthday = "2004-08-21" }, done = true });

        var answers = await (await client.GetAsync("/api/v1/me/quests/who-dis/answers")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("Rao", answers.GetProperty("lastName").GetString());
        Assert.Equal("2004-08-21", answers.GetProperty("birthday").GetString());

        var done = await (await client.PostJsonAsync("/api/v1/me/quests/who-dis/complete", new { })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(50, done.GetProperty("xpEarned").GetInt32());
        Assert.Equal(1, done.GetProperty("progress").GetProperty("completed").GetInt32());
        Assert.Equal(90, done.GetProperty("progress").GetProperty("xp").GetInt32());

        var again = await (await client.PostJsonAsync("/api/v1/me/quests/who-dis/complete", new { })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(0, again.GetProperty("xpEarned").GetInt32());
    }

    [Fact]
    public async Task Quest_errors_use_the_problem_shape()
    {
        var client = api.CreateLearnerClient();

        Assert.Equal("quest-not-found", await (await client.GetAsync("/api/v1/me/quests/nope/answers")).ProblemCodeAsync(HttpStatusCode.NotFound));
        Assert.Equal("quest-locked", await (await client.PostJsonAsync("/api/v1/me/quests/vibe-check/complete", new { })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation", await (await client.PutJsonAsync("/api/v1/me/quests/who-dis/parts/1", new { values = new[] { 1 }, done = true })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Drafts_round_trip()
    {
        var client = api.CreateLearnerClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutJsonAsync("/api/v1/drafts/quest:work-lore:2", new { chapters = new[] { "BPO" } })).StatusCode);
        var draft = await (await client.GetAsync("/api/v1/drafts/quest:work-lore:2")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("BPO", draft.GetProperty("chapters")[0].GetString());
        Assert.Equal("draft-not-found", await (await client.GetAsync("/api/v1/drafts/none")).ProblemCodeAsync(HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Programme_code_flow()
    {
        const string code = "SKM-MUM-26-0417";
        var admin = api.CreateLearnerClient(out _, "Admin", "tenant-admin");
        var learner = api.CreateLearnerClient();

        var programme = new
        {
            code,
            name = "Customer Support Associate",
            partner = "NSQF Level 4 · Aarambh Foundation × Skill India",
            centre = "Andheri West, Mumbai",
            batchStarts = "2026-10-12",
            duration = "3 months · 240 hrs",
            stipend = "₹5,000 / month",
            marksFrom = "12",
            minEnglish = 50,
            minMaths = 50,
            eligibility = "Class 12 · 50%+ Eng & Maths",
        };
        Assert.Equal("forbidden", await (await learner.PutJsonAsync($"/api/v1/admin/programmes/{code}", programme)).ProblemCodeAsync(HttpStatusCode.Forbidden));
        await (await admin.PutJsonAsync($"/api/v1/admin/programmes/{code}", programme)).JsonAsync(HttpStatusCode.OK);

        var found = await (await learner.GetAsync($"/api/v1/programmes/{code.ToLowerInvariant()}")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("2026-10-12", found.GetProperty("batchStarts").GetString());

        var branch = await (await learner.GetAsync("/api/v1/banks/ifsc/HDFC0000240")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("HDFC Bank", branch.GetProperty("bank").GetString());

        var submission = new
        {
            code,
            marksFrom = "12",
            english = "62",
            maths = "58",
            proficiency = new Dictionary<string, string> { ["Excel"] = "good" },
            bank = new { holder = "Asha Rao", type = "savings", account = "50100234567890", routing = "HDFC0000240", upi = "" },
        };
        var result = await (await learner.PostJsonAsync("/api/v1/me/programme", submission)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(100, result.GetProperty("xpEarned").GetInt32());
        Assert.Equal("90", result.GetProperty("accountTail").GetString());
        Assert.Equal("already-enrolled", await (await learner.PostJsonAsync("/api/v1/me/programme", submission)).ProblemCodeAsync(HttpStatusCode.Conflict));

        var stored = await api.QueryAsync(db => db.Enrolments.AsNoTracking().SingleAsync(e => e.ProgrammeCode == code));
        Assert.StartsWith("v1:", stored.BankAccountEncrypted, StringComparison.Ordinal);
        Assert.DoesNotContain("50100234567890", stored.BankAccountEncrypted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Five_wrong_programme_codes_then_429()
    {
        var client = api.CreateLearnerClient();
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal("programme-not-found", await (await client.GetAsync("/api/v1/programmes/WRONG-" + i)).ProblemCodeAsync(HttpStatusCode.NotFound));
        }

        Assert.Equal("too-many-tries", await (await client.GetAsync("/api/v1/programmes/WRONG")).ProblemCodeAsync(HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Profile_edit_photo_and_reveal()
    {
        var client = api.CreateLearnerClient();

        var saved = await (await client.PutJsonAsync("/api/v1/profile", new { skills = new[] { "Sales", "Excel" }, gender = "female", dob = "2004-08-21" })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(10, saved.GetProperty("xpEarned").GetInt32());
        Assert.Equal("•• ••• 2004", saved.GetProperty("profile").GetProperty("privateFields")[3].GetProperty("masked").GetString());

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "photo", "me.png");
        var uploaded = await (await client.PostAsync("/api/v1/profile/photo", form)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(5, uploaded.GetProperty("xpEarned").GetInt32());
        var photoUrl = uploaded.GetProperty("profile").GetProperty("photoUrl").GetString()!;

        var photo = await client.GetAsync(photoUrl);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await photo.Content.ReadAsByteArrayAsync());

        var reveal = await (await client.PostJsonAsync("/api/v1/profile/reveal", new { field = "dob" })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("21 Aug 2004", reveal.GetProperty("value").GetString());
        Assert.Equal("validation", await (await client.PostJsonAsync("/api/v1/profile/reveal", new { field = "password" })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task DigiLocker_sandbox_link_and_unlink()
    {
        var client = api.CreateLearnerClient();

        Assert.Equal(JsonValueKind.Null, (await (await client.GetAsync("/api/v1/me/identity")).JsonAsync(HttpStatusCode.OK)).ValueKind);
        var start = await (await client.PostJsonAsync("/api/v1/me/identity/digilocker/start", new { doc = "aadhaar" })).JsonAsync(HttpStatusCode.OK);
        Assert.StartsWith("/quests/who-dis", start.GetProperty("authorizeUrl").GetString(), StringComparison.Ordinal);

        var linked = await (await client.PostJsonAsync("/api/v1/me/identity/digilocker/link", new { doc = "aadhaar", code = "ok" })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("XXXX XXXX 4821", linked.GetProperty("masked").GetString());
        Assert.Equal("4821", (await (await client.GetAsync("/api/v1/me/identity")).JsonAsync(HttpStatusCode.OK)).GetProperty("last4").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/me/identity")).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await (await client.GetAsync("/api/v1/me/identity")).JsonAsync(HttpStatusCode.OK)).ValueKind);
    }

    [Fact]
    public async Task Trophies_link_check_upload_and_import()
    {
        var client = api.CreateLearnerClient();

        var verified = await (await client.PostJsonAsync("/api/v1/me/trophies/check-link", new { url = "https://www.credly.com/verify/abc" })).JsonAsync(HttpStatusCode.OK);
        Assert.True(verified.GetProperty("verified").GetBoolean());
        Assert.Equal("link-timeout", await (await client.PostJsonAsync("/api/v1/me/trophies/check-link", new { url = "https://example.com/fail" })).ProblemCodeAsync(HttpStatusCode.GatewayTimeout));

        using var form = new MultipartFormDataContent { { new ByteArrayContent("%PDF-1.7\n"u8.ToArray()), "file", "nptel.pdf" } };
        var upload = await (await client.PostAsync("/api/v1/me/trophies/files", form)).JsonAsync(HttpStatusCode.OK);
        Assert.True(Guid.TryParse(upload.GetProperty("fileId").GetString(), out _));

        var imported = await (await client.PostJsonAsync("/api/v1/me/trophies/digilocker", new { })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(2, imported.GetArrayLength());
    }

    [Fact]
    public async Task Feedback_xp_once_a_month_and_skill_search()
    {
        var client = api.CreateLearnerClient();
        var feedback = new { mood = 5, tags = new[] { "fun" }, comment = "Nice", screen = "/quests" };

        Assert.Equal(5, (await (await client.PostJsonAsync("/api/v1/me/feedback", feedback)).JsonAsync(HttpStatusCode.OK)).GetProperty("xp").GetInt32());
        Assert.Equal(0, (await (await client.PostJsonAsync("/api/v1/me/feedback", feedback)).JsonAsync(HttpStatusCode.OK)).GetProperty("xp").GetInt32());

        var skills = await (await client.GetAsync("/api/v1/skills/esco?q=excel&lang=en&limit=5")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("excel", skills.GetProperty("results")[0].GetProperty("label").GetString());
        Assert.Equal("esco-unavailable", await (await client.GetAsync("/api/v1/skills/esco?q=esco-down")).ProblemCodeAsync(HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public async Task A_retried_post_with_the_same_idempotency_key_replays_the_first_response()
    {
        var client = api.CreateLearnerClient();
        var feedback = new { mood = 4, tags = new[] { "fun" }, comment = "Retry me", screen = "/home" };

        async Task<HttpResponseMessage> SendAsync(string key, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/feedback") { Content = JsonContent.Create(body) };
            request.Headers.Add("Idempotency-Key", key);
            return await client.SendAsync(request);
        }

        var first = await SendAsync("feedback-1", feedback);
        Assert.Equal(5, (await first.JsonAsync(HttpStatusCode.OK)).GetProperty("xp").GetInt32());
        Assert.False(first.Headers.Contains("Idempotent-Replayed"));

        var retry = await SendAsync("feedback-1", feedback);
        Assert.Equal(5, (await retry.JsonAsync(HttpStatusCode.OK)).GetProperty("xp").GetInt32());
        Assert.Equal("true", retry.Headers.GetValues("Idempotent-Replayed").Single());

        Assert.Equal(0, (await (await client.PostJsonAsync("/api/v1/me/feedback", feedback)).JsonAsync(HttpStatusCode.OK)).GetProperty("xp").GetInt32());
        Assert.Equal("idempotency-key-reused", await (await SendAsync("feedback-1", new { mood = 1, tags = Array.Empty<string>(), comment = "Other", screen = "/home" })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("invalid-idempotency-key", await (await SendAsync("not a valid key!", feedback)).ProblemCodeAsync(HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Preferences_round_trip()
    {
        var client = api.CreateLearnerClient();
        await (await client.PutJsonAsync("/api/v1/me/preferences", new { sounds = false, voice = true, language = "hi-IN" })).JsonAsync(HttpStatusCode.OK);
        var prefs = await (await client.GetAsync("/api/v1/me/preferences")).JsonAsync(HttpStatusCode.OK);
        Assert.False(prefs.GetProperty("sounds").GetBoolean());
        Assert.Equal("hi-IN", prefs.GetProperty("language").GetString());
    }

    [Fact]
    public async Task Events_create_complete_and_erase_the_learner_once_each()
    {
        var id = Guid.NewGuid();
        var registered = Guid.NewGuid();
        var payload = new { accountId = id, firstName = "Ravi", lastName = "Kumar", email = "ravi@example.com", phone = (string?)null, market = "IN", source = "email", tenantId = LearnerApiFactory.Tenant };

        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("identity.learner.registered", registered, payload));
        Assert.Equal(DispatchOutcome.Duplicate, await api.DeliverAsync("identity.learner.registered", registered, payload));
        Assert.Equal(DispatchOutcome.Ignored, await api.DeliverAsync("someone.else.happened", Guid.NewGuid(), new { }));

        var learner = await api.QueryAsync(db => db.Learners.AsNoTracking().SingleAsync(l => l.Id == id));
        Assert.Equal(("Ravi Kumar", "ravi@example.com"), (learner.FullName, learner.Email));

        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", api.Token(id, "Ravi", ["learner"]));
        var answers = await (await client.GetAsync("/api/v1/me/quests/who-dis/answers")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("Kumar", answers.GetProperty("lastName").GetString());

        var progress = await (await client.GetAsync($"/internal/learners/{id}/profile-progress")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(18, progress.GetProperty("total").GetInt32());

        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("identity.learner.erased", Guid.NewGuid(), new { accountId = id, tenantId = LearnerApiFactory.Tenant }));
        Assert.False(await api.QueryAsync(db => db.Learners.AnyAsync(l => l.Id == id)));
        Assert.False(await api.QueryAsync(db => db.QuestAnswers.AnyAsync(a => a.LearnerId == id)));
    }
}
