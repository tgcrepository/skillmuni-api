using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillMuni.Messaging.Inbox;
using SkillMuni.Testing;

namespace Assessment.IntegrationTests;

[Collection(AssessmentApiCollection.Name)]
public sealed class AssessmentEndpointsTests(AssessmentApiFactory api)
{
    private const string Base = "/api/v1/assessments/career";

    /// <summary>36 answers: the first <paramref name="count"/> with <paramref name="notch"/>, then null.</summary>
    private static object?[] Answers(int count, int notch = 1) =>
        Enumerable.Range(0, 36).Select(i => i < count ? (object?)notch : null).ToArray();

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, object?[] answers, int current) =>
        client.PutJsonAsync($"{Base}/answers", new { answers, current });

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, object?[] answers) =>
        client.PostJsonAsync($"{Base}/submit", new { answers });

    private Task<List<(string Type, JsonElement Payload)>> EventsAsync(Guid learnerId) =>
        api.QueryAsync(async db =>
            (await db.OutboxMessages.AsNoTracking().OrderBy(m => m.OccurredAt).ToListAsync())
                .Select(m => (Type: m.EventType, Payload: JsonDocument.Parse(m.Payload).RootElement.Clone()))
                .Where(e => e.Payload.GetProperty("learnerId").GetGuid() == learnerId)
                .ToList());

    [Fact]
    public async Task A_new_learner_gets_the_intro_state()
    {
        var client = api.CreateLearnerClient(out _);
        var state = await (await client.GetAsync(Base)).JsonAsync(HttpStatusCode.OK);

        Assert.Equal("new", state.GetProperty("status").GetString());
        Assert.Equal(36, state.GetProperty("pairs").GetArrayLength());
        var first = state.GetProperty("pairs")[0];
        Assert.Equal((1, "A", "B"), (first.GetProperty("n").GetInt32(), first.GetProperty("this").GetProperty("driver").GetString(), first.GetProperty("that").GetProperty("driver").GetString()));
        var island = state.GetProperty("islands")[1];
        Assert.Equal(("Main Character", "🎬", 10, 18), (island.GetProperty("name").GetString(), island.GetProperty("emoji").GetString(), island.GetProperty("from").GetInt32(), island.GetProperty("to").GetInt32()));
        Assert.All(state.GetProperty("answers").EnumerateArray(), a => Assert.Equal(JsonValueKind.Null, a.ValueKind));
        Assert.Equal(36, state.GetProperty("answers").GetArrayLength());
        Assert.Equal((0, 0, true, true, 0), (state.GetProperty("current").GetInt32(), state.GetProperty("xp").GetInt32(), state.GetProperty("earnsXp").GetBoolean(), state.GetProperty("remind").GetBoolean(), state.GetProperty("attempts").GetInt32()));
    }

    [Fact]
    public async Task Auto_save_resumes_and_pays_an_island_once()
    {
        var client = api.CreateLearnerClient(out var learnerId);
        var answers = Answers(9);
        answers[3] = "skip";

        var saved = await (await SaveAsync(client, answers, 9)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal((0, 0), (saved.GetProperty("xp").GetInt32(), saved.GetProperty("xpEarned").GetInt32()));

        answers[3] = 2;
        saved = await (await SaveAsync(client, answers, 9)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal((25, 25), (saved.GetProperty("xp").GetInt32(), saved.GetProperty("xpEarned").GetInt32()));
        saved = await (await SaveAsync(client, answers, 9)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal((25, 0), (saved.GetProperty("xp").GetInt32(), saved.GetProperty("xpEarned").GetInt32()));

        var state = await (await client.GetAsync(Base)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(("in-progress", 9, 25), (state.GetProperty("status").GetString(), state.GetProperty("current").GetInt32(), state.GetProperty("xp").GetInt32()));
        Assert.Equal(2, state.GetProperty("answers")[3].GetInt32());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("answers")[9].ValueKind);

        var cleared = Assert.Single(await EventsAsync(learnerId));
        Assert.Equal("assessment.island.cleared", cleared.Type);
        Assert.Equal((1, 1, true), (cleared.Payload.GetProperty("attempt").GetInt32(), cleared.Payload.GetProperty("island").GetInt32(), cleared.Payload.GetProperty("firstAttempt").GetBoolean()));
    }

    [Fact]
    public async Task Skip_round_trips_as_the_string()
    {
        var client = api.CreateLearnerClient(out _);
        var answers = Answers(2);
        answers[1] = "skip";
        await (await SaveAsync(client, answers, 2)).JsonAsync(HttpStatusCode.OK);

        var state = await (await client.GetAsync(Base)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal("skip", state.GetProperty("answers")[1].GetString());
    }

    [Fact]
    public async Task Bad_saves_are_refused()
    {
        var client = api.CreateLearnerClient(out _);
        var answers = Answers(1);
        answers[0] = 7;
        Assert.Equal("malformed-request", await (await SaveAsync(client, answers, 0)).ProblemCodeAsync(HttpStatusCode.BadRequest));
        Assert.Equal("validation", await (await SaveAsync(client, Answers(1).Take(5).ToArray(), 0)).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation", await (await SaveAsync(client, Answers(1), 40)).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Submit_needs_every_pair_answered()
    {
        var client = api.CreateLearnerClient(out _);
        var answers = Answers(36);
        answers[30] = "skip";
        Assert.Equal("incomplete", await (await SubmitAsync(client, answers)).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("incomplete", await (await SubmitAsync(client, Answers(35))).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Submit_reveals_the_archetype_and_pays_100_on_the_first_attempt()
    {
        var client = api.CreateLearnerClient(out var learnerId);
        Assert.Equal("result-not-found", await (await client.GetAsync($"{Base}/result")).ProblemCodeAsync(HttpStatusCode.NotFound));

        await (await SaveAsync(client, Answers(18, notch: 0), 18)).JsonAsync(HttpStatusCode.OK);
        var result = await (await SubmitAsync(client, Answers(36, notch: 0))).JsonAsync(HttpStatusCode.OK);

        Assert.Equal((1, 100, 2), (result.GetProperty("attempt").GetInt32(), result.GetProperty("xpEarned").GetInt32(), result.GetProperty("mandatoryCourses").GetInt32()));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", result.GetProperty("date").GetString());
        var scores = result.GetProperty("scores");
        Assert.Equal(new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I" }, scores.EnumerateObject().Select(p => p.Name));
        Assert.Equal(108, scores.EnumerateObject().Sum(p => p.Value.GetInt32()));
        Assert.Equal(9, result.GetProperty("ranking").GetArrayLength());
        Assert.Equal(9, result.GetProperty("drivers").GetArrayLength());
        Assert.Equal("Material Rewards", result.GetProperty("drivers")[0].GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("previous").ValueKind);
        var archetype = result.GetProperty("archetype");
        Assert.Equal(archetype.GetProperty("name").GetString(), result.GetProperty("attempts")[0].GetProperty("archetype").GetString());
        Assert.Equal(archetype.GetProperty("id").GetString() == "strategist", archetype.TryGetProperty("eitherOf", out _));

        var state = await (await client.GetAsync(Base)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(("done", false, 1, 0), (state.GetProperty("status").GetString(), state.GetProperty("earnsXp").GetBoolean(), state.GetProperty("attempts").GetInt32(), state.GetProperty("xp").GetInt32()));

        var events = await EventsAsync(learnerId);
        Assert.Equal(3, events.Count(e => e.Type == "assessment.island.cleared"));
        var submitted = Assert.Single(events, e => e.Type == "assessment.attempt.submitted").Payload;
        Assert.True(submitted.GetProperty("firstAttempt").GetBoolean());
        Assert.Equal(archetype.GetProperty("name").GetString(), submitted.GetProperty("archetypeName").GetString());
        Assert.Equal(archetype.GetProperty("summary").GetString(), submitted.GetProperty("archetypeSummary").GetString());
        Assert.Equal(3, submitted.GetProperty("topDrivers").GetArrayLength());

        var stored = await api.QueryAsync(db => db.Attempts.AsNoTracking().SingleAsync(a => a.LearnerId == learnerId));
        Assert.Equal(108, stored.Scores.Total);
    }

    [Fact]
    public async Task Retake_restart_and_results_by_attempt()
    {
        var client = api.CreateLearnerClient(out var learnerId);
        await (await SubmitAsync(client, Answers(36, notch: 0))).JsonAsync(HttpStatusCode.OK);

        var retake = await (await client.PostJsonAsync($"{Base}/retake", new { })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(("done", false, 0), (retake.GetProperty("status").GetString(), retake.GetProperty("earnsXp").GetBoolean(), retake.GetProperty("xp").GetInt32()));

        var saved = await (await SaveAsync(client, Answers(9, notch: 3), 9)).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(0, saved.GetProperty("xpEarned").GetInt32());

        var restart = await (await client.PostJsonAsync($"{Base}/restart", new { })).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(("done", 0), (restart.GetProperty("status").GetString(), restart.GetProperty("current").GetInt32()));

        var second = await (await SubmitAsync(client, Answers(36, notch: 3))).JsonAsync(HttpStatusCode.OK);
        Assert.Equal((2, 0), (second.GetProperty("attempt").GetInt32(), second.GetProperty("xpEarned").GetInt32()));
        Assert.Equal(JsonValueKind.Object, second.GetProperty("previous").ValueKind);
        Assert.Equal(new[] { 2, 1 }, second.GetProperty("attempts").EnumerateArray().Select(a => a.GetProperty("n").GetInt32()));

        var first = await (await client.GetAsync($"{Base}/result?attempt=1")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal((1, 100), (first.GetProperty("attempt").GetInt32(), first.GetProperty("xpEarned").GetInt32()));
        var latest = await (await client.GetAsync($"{Base}/result?attempt=abc")).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(2, latest.GetProperty("attempt").GetInt32());

        var submitted = (await EventsAsync(learnerId)).Where(e => e.Type == "assessment.attempt.submitted").ToList();
        Assert.Equal(new[] { true, false }, submitted.Select(e => e.Payload.GetProperty("firstAttempt").GetBoolean()));
        var islandOnRetake = (await EventsAsync(learnerId)).Last(e => e.Type == "assessment.island.cleared");
        Assert.False(islandOnRetake.Payload.GetProperty("firstAttempt").GetBoolean());
    }

    [Fact]
    public async Task Submit_with_an_idempotency_key_runs_once()
    {
        var client = api.CreateLearnerClient(out var learnerId);
        var key = Guid.NewGuid().ToString();

        async Task<HttpResponseMessage> Send()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Base}/submit") { Content = JsonContent.Create(new { answers = Answers(36) }) };
            request.Headers.Add("Idempotency-Key", key);
            return await client.SendAsync(request);
        }

        var first = await (await Send()).JsonAsync(HttpStatusCode.OK);
        var retry = await Send();
        Assert.Equal("true", retry.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.Equal(first.GetProperty("attempt").GetInt32(), (await retry.JsonAsync(HttpStatusCode.OK)).GetProperty("attempt").GetInt32());
        Assert.Equal(1, await api.QueryAsync(db => db.Attempts.CountAsync(a => a.LearnerId == learnerId)));
    }

    [Fact]
    public async Task Remind_me_tomorrow_is_stored()
    {
        var client = api.CreateLearnerClient(out _);
        var off = await (await client.PutJsonAsync($"{Base}/remind", new { remind = false })).JsonAsync(HttpStatusCode.OK);
        Assert.False(off.GetProperty("remind").GetBoolean());
        Assert.False((await (await client.GetAsync(Base)).JsonAsync(HttpStatusCode.OK)).GetProperty("remind").GetBoolean());
        Assert.Equal("validation", await (await client.PutJsonAsync($"{Base}/remind", new { })).ProblemCodeAsync(HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task Skill_stats_show_the_empty_state()
    {
        var client = api.CreateLearnerClient(out _);
        var stats = await (await client.GetAsync("/api/v1/stats/assessments")).JsonAsync(HttpStatusCode.OK);

        Assert.Equal(0, stats.GetProperty("results").GetArrayLength());
        Assert.Equal((20, 20), (stats.GetProperty("available").GetInt32(), stats.GetProperty("firstCardXp").GetInt32()));
        Assert.Equal("Digital Skills", stats.GetProperty("recommended").GetProperty("title").GetString());
        Assert.Equal("/assessments/digital-skills", stats.GetProperty("recommended").GetProperty("url").GetString());
    }

    [Fact]
    public async Task Requests_without_a_session_are_401()
    {
        var anonymous = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Base)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/stats/assessments")).StatusCode);
    }

    [Fact]
    public async Task Registration_starts_a_journey_and_erasure_deletes_it()
    {
        var client = api.CreateLearnerClient(out var learnerId);
        var registered = new { accountId = learnerId, firstName = "Asha", lastName = "K", market = "IN", tenantId = TestTokens.Tenant };
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("identity.learner.registered", registered));
        Assert.True(await api.QueryAsync(db => db.Journeys.AnyAsync(j => j.Id == learnerId)));

        await (await SubmitAsync(client, Answers(36))).JsonAsync(HttpStatusCode.OK);
        Assert.Equal(DispatchOutcome.Handled, await api.DeliverAsync("identity.learner.erased", new { accountId = learnerId, tenantId = TestTokens.Tenant }));

        Assert.False(await api.QueryAsync(db => db.Journeys.AnyAsync(j => j.Id == learnerId)));
        Assert.False(await api.QueryAsync(db => db.Attempts.AnyAsync(a => a.LearnerId == learnerId)));
        // The erased account's still-valid token no longer recreates an empty journey (tombstone → 410).
        Assert.Equal("learner-erased", await (await client.GetAsync(Base)).ProblemCodeAsync(HttpStatusCode.Gone));
        Assert.False(await api.QueryAsync(db => db.Journeys.AnyAsync(j => j.Id == learnerId)));
    }
}
