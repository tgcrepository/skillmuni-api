using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Careers.Application.Abstractions;
using Careers.Domain;
using Microsoft.EntityFrameworkCore;
using SkillMuni.Testing;

namespace Careers.IntegrationTests;

/// <summary>API-075–089 against MySQL.</summary>
[Collection(CareersApiCollection.Name)]
public sealed class JobsEndpointsTests(CareersApiFactory api)
{
    private static readonly byte[] Pdf = "%PDF-1.7\n1 0 obj\n"u8.ToArray();

    private static string Filters(object filters) => Uri.EscapeDataString(JsonSerializer.Serialize(filters));

    private static async Task<string> UploadCvAsync(HttpClient client)
    {
        var file = new ByteArrayContent(Pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        using var form = new MultipartFormDataContent { { file, "file", "Yash_Pohare_CV_2026.pdf" } };
        var cv = await (await client.PostAsync("/api/v1/jobs/cv", form)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("Yash_Pohare_CV_2026.pdf", cv.GetProperty("name").GetString());
        Assert.Equal("Uploaded today · 1 KB", cv.GetProperty("meta").GetString());
        return cv.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Summary_and_lists()
    {
        var me = Guid.NewGuid();
        api.Learners.SetPercent(me, 72);
        var client = api.ClientFor(me);

        var summary = await (await client.GetAsync("/api/v1/jobs/summary")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(128, summary.GetProperty("available").GetInt32());
        Assert.Equal(0, summary.GetProperty("matching").GetInt32());
        Assert.Equal(72, summary.GetProperty("profilePercent").GetInt32());
        Assert.Equal("Your Flex Era", summary.GetProperty("missing")[0].GetString());
        Assert.Equal(0, summary.GetProperty("unlockAt").GetInt32());

        var list = await (await client.GetAsync($"/api/v1/jobs/list/available?q=&place=Pune%20and%20Remote&sort=newest&limit=5&filters={Filters(new { workMode = new[] { "onsite" } })}")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(5, list.GetProperty("items").GetArrayLength());
        Assert.True(list.GetProperty("total").GetInt32() > 5);
        Assert.Equal(JsonValueKind.Null, list.GetProperty("note").ValueKind);
        Assert.Equal("sector", list.GetProperty("facets")[0].GetProperty("key").GetString());
        var first = list.GetProperty("items")[0];
        Assert.Equal("onsite", first.GetProperty("workMode").GetString());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("match").ValueKind);

        var none = await (await client.GetAsync($"/api/v1/jobs/list/available?filters={Filters(new { sector = new[] { "Retail" }, city = new[] { "Goa" } })}")).JsonAsync(HttpStatusCode.OK);
        if (none.GetProperty("total").GetInt32() == 0)
        {
            Assert.True(none.GetProperty("suggestions").GetArrayLength() > 0);
        }

        Assert.Equal("tab-not-found", await (await client.GetAsync("/api/v1/jobs/list/galaxy")).ProblemCodeAsync(HttpStatusCode.NotFound));
        Assert.Equal("job-not-found", await (await client.GetAsync("/api/v1/jobs/job/job-999")).ProblemCodeAsync(HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Matching_needs_the_learners_skills()
    {
        var me = Guid.NewGuid();
        api.Learners.SetProfile(me, new LearnerCareerProfile("Yash Pohare", "+91 98•••• 0697 · Pune", "Pune", "Maharashtra", false, [new("Communication", "verified"), new("Customer service", "assessed"), new("English", "verified")]));
        var client = api.ClientFor(me);

        var matching = await (await client.GetAsync("/api/v1/jobs/list/matching?limit=50")).JsonAsync(HttpStatusCode.OK);
        Assert.True(matching.GetProperty("total").GetInt32() > 0);
        Assert.Equal("match", matching.GetProperty("facets")[0].GetProperty("key").GetString());
        var top = matching.GetProperty("items")[0].GetProperty("match");
        Assert.True(top.GetProperty("percent").GetInt32() >= 60);
        Assert.StartsWith("Skills: ", top.GetProperty("reasons")[0].GetProperty("text").GetString());

        var job = await (await client.GetAsync("/api/v1/jobs/job/job-1")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("Customer Service Associate", job.GetProperty("title").GetString());
        Assert.Equal("Location: Pune, where you live", job.GetProperty("match").GetProperty("reasons")[1].GetProperty("text").GetString());

        var info = await (await client.GetAsync("/api/v1/jobs/job/job-1/apply")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("Yash Pohare", info.GetProperty("name").GetString());
        Assert.Equal(2, info.GetProperty("questions").GetArrayLength());
    }

    [Fact]
    public async Task Save_apply_withdraw()
    {
        var me = Guid.NewGuid();
        var client = api.ClientFor(me);

        var saved = await (await client.PutJsonAsync("/api/v1/jobs/job/job-2/saved", new { saved = true })).JsonAsync(HttpStatusCode.OK);
        Assert.True(saved.GetProperty("saved").GetBoolean());
        Assert.Equal(0, saved.GetProperty("savedDays").GetInt32());
        var bookmarked = await (await client.GetAsync("/api/v1/jobs/list/bookmarked")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("1 saved job · 1 closing soon", bookmarked.GetProperty("note").GetString());

        var cvId = await UploadCvAsync(client);
        var info = await (await client.GetAsync("/api/v1/jobs/job/job-2/apply")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(cvId, info.GetProperty("cvs")[0].GetProperty("id").GetString());
        Assert.Equal("Yash", info.GetProperty("name").GetString());

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs/job/job-2/apply")
        {
            Content = JsonContent(new { cvId, answers = new Dictionary<string, string> { ["q1"] = "Yes", ["q2"] = "Immediately" } }),
        };
        request.Headers.Add("Idempotency-Key", "apply-job-2");
        var response = await client.SendAsync(request);
        var application = await response.JsonAsync(HttpStatusCode.Created);
        var id = application.GetProperty("id").GetString();
        Assert.Equal($"/api/v1/jobs/applications/{id}", response.Headers.Location?.ToString());
        Assert.Equal("applied", application.GetProperty("status").GetString());
        Assert.Equal("Are you comfortable with rotational shifts, including nights?", application.GetProperty("answers")[0].GetProperty("q").GetString());
        Assert.Equal(5, application.GetProperty("timeline").GetArrayLength());

        using var replay = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs/job/job-2/apply")
        {
            Content = JsonContent(new { cvId, answers = new Dictionary<string, string> { ["q1"] = "Yes", ["q2"] = "Immediately" } }),
        };
        replay.Headers.Add("Idempotency-Key", "apply-job-2");
        var replayed = await client.SendAsync(replay);
        Assert.Equal(id, (await replayed.JsonAsync(HttpStatusCode.Created)).GetProperty("id").GetString());
        Assert.Equal("already-applied", await (await client.PostJsonAsync("/api/v1/jobs/job/job-2/apply", new { cvId, answers = new { } })).ProblemCodeAsync(HttpStatusCode.Conflict));
        Assert.Equal("validation", await (await client.PostJsonAsync("/api/v1/jobs/job/job-3/apply", new { cvId = "" })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("cv-not-found", await (await client.PostJsonAsync("/api/v1/jobs/job/job-3/apply", new { cvId = Guid.NewGuid() })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));

        var events = await api.QueryAsync(db => db.OutboxMessages.Where(m => m.EventType == "careers.application.sent" && m.Payload.Contains(me.ToString())).CountAsync());
        Assert.Equal(1, events);

        var detail = await (await client.GetAsync($"/api/v1/jobs/applications/{id}")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("15 Oct", detail.GetProperty("job").GetProperty("appliedOn").GetString());

        var applied = await (await client.GetAsync("/api/v1/jobs/applications")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(1, applied.GetProperty("total").GetInt32());
        Assert.Equal("1 application", applied.GetProperty("note").GetString());

        var withdrawn = await (await client.PostJsonAsync($"/api/v1/jobs/applications/{id}/withdraw", new { })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("withdrawn", withdrawn.GetProperty("status").GetString());
        Assert.Equal("You withdrew on 15 Oct", withdrawn.GetProperty("updated").GetString());
        Assert.Equal("reapply-cool-off", await (await client.PostJsonAsync("/api/v1/jobs/job/job-2/apply", new { cvId })).ProblemCodeAsync(HttpStatusCode.Conflict));
        Assert.Equal("application-not-found", await (await client.GetAsync($"/api/v1/jobs/applications/{Guid.NewGuid()}")).ProblemCodeAsync(HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task Cv_upload_refuses_other_files()
    {
        var client = api.ClientFor(Guid.NewGuid());
        using var form = new MultipartFormDataContent { { new ByteArrayContent("just text"u8.ToArray()), "file", "cv.pdf" } };
        Assert.Equal("unsupported-type", await (await client.PostAsync("/api/v1/jobs/cv", form)).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        using var empty = new MultipartFormDataContent { { new StringContent("x"), "note" } };
        Assert.Equal("file-missing", await (await client.PostAsync("/api/v1/jobs/cv", empty)).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Invitations_list_respond_and_expire()
    {
        var me = Guid.NewGuid();
        var now = CareersApiFactory.Today.UtcDateTime;
        var invite = Invitation.Create(TestTokens.Tenant, me, "job-1", "Priya S.", "PS", "Recruiter, Teleperformance", "apply", null, "Hi Yash, we'd love you to apply.", new DateOnly(2026, 10, 22), now);
        var interview = Invitation.Create(TestTokens.Tenant, me, "job-2", "Rahul K.", "RK", "Recruiter, HDFC Life", "interview", now.AddDays(5), "Video interview?", new DateOnly(2026, 10, 20), now.AddMinutes(1));
        var expired = Invitation.Create(TestTokens.Tenant, me, "job-6", "Kavya R.", "KR", "HR, Taj Hotels", "apply", null, "Front desk openings", new DateOnly(2026, 10, 1), now.AddMinutes(2));
        await api.QueryAsync(async db =>
        {
            db.Invitations.AddRange(invite, interview, expired);
            return await db.SaveChangesAsync();
        });
        var client = api.ClientFor(me);

        var list = await (await client.GetAsync("/api/v1/jobs/invitations")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("2 new · 3 total", list.GetProperty("note").GetString());
        Assert.Equal(interview.Id.ToString(), list.GetProperty("items")[0].GetProperty("id").GetString());
        Assert.Equal("2026-10-20T11:30:00+05:30", list.GetProperty("items")[0].GetProperty("interviewAt").GetString());
        Assert.Equal("expired", list.GetProperty("items")[2].GetProperty("status").GetString());
        Assert.Equal("status", list.GetProperty("facets")[0].GetProperty("key").GetString());

        var filtered = await (await client.GetAsync($"/api/v1/jobs/invitations?filters={Filters(new { inviteType = new[] { "interview" } })}")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(1, filtered.GetProperty("total").GetInt32());

        var detail = await (await client.GetAsync($"/api/v1/jobs/invitations/{invite.Id}")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(7, detail.GetProperty("daysLeft").GetInt32());
        Assert.Equal("22 Oct", detail.GetProperty("respondBy").GetString());

        var accepted = await (await client.PostJsonAsync($"/api/v1/jobs/invitations/{interview.Id}/accept", new { note = "" })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("accepted", accepted.GetProperty("status").GetString());
        var suggested = await (await client.PostJsonAsync($"/api/v1/jobs/invitations/{invite.Id}/suggest", new { note = "Monday works better" })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("new", suggested.GetProperty("status").GetString());
        Assert.Equal("declined", (await (await client.PostJsonAsync($"/api/v1/jobs/invitations/{invite.Id}/decline", new { note = "" })).JsonAsync(HttpStatusCode.OK)).GetProperty("status").GetString());
        Assert.Equal("invitation-expired", await (await client.PostJsonAsync($"/api/v1/jobs/invitations/{expired.Id}/accept", new { note = "" })).ProblemCodeAsync(HttpStatusCode.Gone));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostJsonAsync($"/api/v1/jobs/invitations/{invite.Id}/maybe", new { note = "" })).StatusCode);
        Assert.Equal("invitation-not-found", await (await client.GetAsync("/api/v1/jobs/invitations/inv-1")).ProblemCodeAsync(HttpStatusCode.NotFound));

        var card = await (await client.GetAsync("/api/v1/launchpad/jobs")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(1, card.GetProperty("interviews").GetInt32());
    }

    [Fact]
    public async Task Alerts()
    {
        var client = api.ClientFor(Guid.NewGuid());
        var alerts = await (await client.GetAsync("/api/v1/jobs/alerts")).JsonAsync(HttpStatusCode.OK);
        Assert.False(alerts.GetProperty("on").GetBoolean());

        var saved = await (await client.PutJsonAsync("/api/v1/jobs/alerts", new { on = true, frequency = "weekly", searches = new[] { "Customer service · Pune" } })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("weekly", saved.GetProperty("frequency").GetString());
        Assert.Equal("Customer service · Pune", (await (await client.GetAsync("/api/v1/jobs/alerts")).JsonAsync(HttpStatusCode.OK)).GetProperty("searches")[0].GetString());
        Assert.Equal("validation", await (await client.PutJsonAsync("/api/v1/jobs/alerts", new { on = true, frequency = "hourly", searches = Array.Empty<string>() })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task No_session_is_401() =>
        Assert.Equal("no-session", await (await api.CreateClient().GetAsync("/api/v1/jobs/summary")).ProblemCodeAsync(HttpStatusCode.Unauthorized));

    private static StringContent JsonContent(object body) =>
        new(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), System.Text.Encoding.UTF8, "application/json");
}
