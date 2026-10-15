using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;

namespace Cv.IntegrationTests;

[Collection(CvApiCollection.Name)]
public sealed class CvEndpointsTests(CvApiFactory api)
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nsample\n%%EOF");

    private static MultipartFormDataContent Form(string fileName, byte[]? bytes = null, string field = "file")
    {
        var content = new ByteArrayContent(bytes ?? Pdf);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { content, field, fileName } };
    }

    private static async Task<string> UploadAsync(HttpClient client, string fileName, byte[]? bytes = null)
    {
        using var form = Form(fileName, bytes);
        var response = await client.PostAsync("/api/v1/cv", form);
        Assert.Equal($"/api/v1/cv/{(await response.JsonAsync(HttpStatusCode.Created)).GetProperty("cvId").GetString()}/status", response.Headers.Location?.ToString());
        return response.Headers.Location!.ToString().Split('/')[4];
    }

    /// <summary>Polls like the parse screen until the worker finishes.</summary>
    private static async Task<JsonElement> WaitAsync(HttpClient client, string cvId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var status = await (await client.GetAsync($"/api/v1/cv/{cvId}/status")).JsonAsync(HttpStatusCode.OK);
            if (status.GetProperty("state").GetString() != "parsing" || DateTime.UtcNow > deadline)
            {
                return status;
            }

            await Task.Delay(200);
        }
    }

    [Fact]
    public async Task Upload_parse_and_summary()
    {
        var client = api.CreateLearnerClient(out var learnerId);
        var cvId = await UploadAsync(client, "asha.pdf");

        var status = await WaitAsync(client, cvId);
        Assert.Equal(("done", 100), (status.GetProperty("state").GetString(), status.GetProperty("percent").GetInt32()));

        // cv.api.ts: `outcome?` and `detail?` are optional, so a clean parse leaves them out (never null).
        Assert.False(status.TryGetProperty("outcome", out _));
        Assert.False(status.TryGetProperty("detail", out _));

        var summary = await (await client.GetAsync($"/api/v1/cv/{cvId}/summary")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(5, summary.GetProperty("questsPrefilled").GetInt32());
        Assert.Equal("Your name", summary.GetProperty("sections")[0].GetProperty("title").GetString());
        var fields = summary.GetProperty("sections").EnumerateArray().SelectMany(s => s.GetProperty("fields").EnumerateArray()).ToList();
        Assert.DoesNotContain(fields, f => f.TryGetProperty("check", out var check) && check.ValueKind == JsonValueKind.Null);
        Assert.DoesNotContain(summary.GetProperty("sections").EnumerateArray(), s => s.TryGetProperty("chips", out var chips) && chips.ValueKind == JsonValueKind.Null);

        var stored = await api.QueryAsync(db => db.Documents.AsNoTracking().SingleAsync(d => d.LearnerId == learnerId));
        Assert.DoesNotContain("14/05/2003", stored.ExtractJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("stuck.pdf", "failed", "parse-failed")]
    [InlineData("afrikaans.pdf", "done", "other-language")]
    [InlineData("taken.pdf", "done", "email-taken")]
    public async Task Parse_outcomes_travel_in_the_status(string fileName, string state, string outcome)
    {
        var client = api.CreateLearnerClient(out _);
        var status = await WaitAsync(client, await UploadAsync(client, fileName));
        Assert.Equal((state, outcome), (status.GetProperty("state").GetString(), status.GetProperty("outcome").GetString()));
    }

    [Fact]
    public async Task A_failed_parse_has_no_summary()
    {
        var client = api.CreateLearnerClient(out _);
        var cvId = await UploadAsync(client, "stuck.pdf");
        await WaitAsync(client, cvId);
        Assert.Equal("cv-parse-failed", await (await client.GetAsync($"/api/v1/cv/{cvId}/summary")).ProblemCodeAsync(HttpStatusCode.Gone));
    }

    [Fact]
    public async Task Refused_files_use_the_s1_and_s2_codes()
    {
        var client = api.CreateLearnerClient(out _);
        using var exe = Form("cv.pdf", [0x4D, 0x5A, 0x90, 0x00]);
        Assert.Equal("unsupported-type", await (await client.PostAsync("/api/v1/cv", exe)).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        using var empty = new MultipartFormDataContent { { new StringContent("x"), "note" } };
        Assert.Equal("file-missing", await (await client.PostAsync("/api/v1/cv", empty)).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Photographed_pages_upload_together()
    {
        var client = api.CreateLearnerClient(out _);
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(jpeg), "pages", "page-1.jpg" },
            { new ByteArrayContent(jpeg), "pages", "page-2.jpg" },
        };
        var created = await (await client.PostAsync("/api/v1/cv", form)).JsonAsync(HttpStatusCode.Created);
        var status = await WaitAsync(client, created.GetProperty("cvId").GetString()!);
        Assert.Equal("done", status.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Another_learners_cv_is_404_and_delete_removes_it()
    {
        var owner = api.CreateLearnerClient(out _);
        var other = api.CreateLearnerClient(out _);
        var cvId = await UploadAsync(owner, "asha.pdf");
        await WaitAsync(owner, cvId);

        Assert.Equal("cv-not-found", await (await other.GetAsync($"/api/v1/cv/{cvId}/status")).ProblemCodeAsync(HttpStatusCode.NotFound));
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/cv/{cvId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/v1/cv/{cvId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/v1/cv/{cvId}/status")).StatusCode);
    }

    [Fact]
    public async Task No_session_is_401()
    {
        using var form = Form("cv.pdf");
        Assert.Equal("no-session", await (await api.CreateClient().PostAsync("/api/v1/cv", form)).ProblemCodeAsync(HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Erasure_removes_every_cv()
    {
        var client = api.CreateLearnerClient(out var learnerId);
        await WaitAsync(client, await UploadAsync(client, "asha.pdf"));

        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("identity.learner.erased", new { accountId = learnerId, tenantId = TestTokens.Tenant }));
        Assert.False(await api.QueryAsync(db => db.Documents.AnyAsync(d => d.LearnerId == learnerId)));
    }
}
